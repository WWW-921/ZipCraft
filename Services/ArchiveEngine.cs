using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SimpleCompress.Models;

namespace SimpleCompress.Services;

/// <summary>
/// 封装 7z.exe 的真实调用：创建、解压、测试、列表。
/// - 独立参数列表，正确处理空格/中文/引号/多选（使用 ArgumentList，不做字符串拼接）。
/// - 解析实时进度与当前文件；取消时终止关联进程并回收。
/// - 用退出码 + 输出 + 文件状态判定成败；错误分类为中文可操作提示。
/// - 日志脱敏，绝不记录明文密码。
/// </summary>
public class ArchiveEngine
{
    private readonly string _sevenZip;

    public ArchiveEngine(string pathTo7z) => _sevenZip = pathTo7z;

    /// <summary>进度回调：percent 可为 null（不确定进度），file 为当前处理文件。</summary>
    public delegate void ProgressHandler(int? percent, string? file);

    // ---------- 压缩 ----------
    public async Task<TaskResult> CompressAsync(CompressRequest req,
        ProgressHandler? progress, CancellationToken ct)
    {
        var args = new List<string>
        {
            "a",
            $"-t{FormatToken(req.Format)}",
            $"-mx{LevelDigit(req.Level)}",
            "-bb1", "-bsp1", "-bso1"
        };

        bool pw = !string.IsNullOrEmpty(req.Password);
        if (pw)
        {
            args.Add($"-p{req.Password}");
            if (req.Format == ArchiveFormat.SevenZip && req.EncryptHeaders)
                args.Add("-mhe=on");
        }

        if (!string.IsNullOrWhiteSpace(req.VolumeSize) &&
            !req.VolumeSize.Equals("none", StringComparison.OrdinalIgnoreCase))
            args.Add($"-v{req.VolumeSize}");

        args.Add(req.OutputArchive);
        foreach (var i in req.Inputs) args.Add(i);

        var res = await RunAsync(args, req.Password, progress, ct).ConfigureAwait(false);
        if (!res.Cancelled)
        {
            res.OutputPath = req.OutputArchive;
            bool produced = File.Exists(req.OutputArchive)
                || File.Exists(req.OutputArchive + ".001")
                || File.Exists(req.OutputArchive + ".zip.001");
            if (res.ExitCode == 0 && produced) res.Success = true;
            else if (res.ExitCode == 0 && !produced) { res.ErrorKind = "Corrupt"; res.ErrorMessage = "引擎报告成功，但未找到输出文件。"; }
            else FinalizeError(res, null);
        }
        return res;
    }

    // ---------- 解压 ----------
    public async Task<TaskResult> ExtractAsync(ExtractRequest req,
        ProgressHandler? progress, CancellationToken ct)
    {
        string overwriteSwitch = req.Conflict switch
        {
            ConflictPolicy.Skip => "-aos",
            _ => "-aoa"
        };

        var entries = TryList(req.Archive, req.Password);
        int unsafeCount = entries.Count(e => PathGuard.IsUnsafe(e.Path));

        // “保留两份”：把目标已存在的同名文件先改名，再全量覆盖解压。
        if (req.Conflict == ConflictPolicy.Rename)
            PreRenameConflicts(req.Destination, entries);

        var args = new List<string>
        {
            req.FlattenPaths ? "e" : "x",
            req.Archive,
            $"-o{req.Destination}",
            overwriteSwitch,
            "-y",
            "-bb1", "-bsp1", "-bso1"
        };

        bool pw = !string.IsNullOrEmpty(req.Password);
        if (pw) args.Add($"-p{req.Password}");

        var res = await RunAsync(args, req.Password, progress, ct).ConfigureAwait(false);
        if (!res.Cancelled)
        {
            res.OutputPath = req.Destination;
            if (unsafeCount > 0)
                res.Log.Add($"注意：{unsafeCount} 个越界/绝对路径条目已由引擎按目标目录规范化写入。");
            if (res.ExitCode == 0)
            {
                res.Success = Directory.Exists(req.Destination);
                if (!res.Success) { res.ErrorKind = "Corrupt"; res.ErrorMessage = "解压结束但未生成目标目录。"; }
            }
            else FinalizeError(res, null);
        }
        return res;
    }

    // ---------- 测试完整性 ----------
    public async Task<TaskResult> TestAsync(string archive, string? password,
        ProgressHandler? progress, CancellationToken ct)
    {
        var args = new List<string> { "t", archive, "-y", "-bb1", "-bsp1", "-bso1" };
        bool pw = !string.IsNullOrEmpty(password);
        if (pw) args.Add($"-p{password}");
        var res = await RunAsync(args, password, progress, ct).ConfigureAwait(false);
        if (!res.Cancelled)
        {
            if (res.ExitCode == 0) res.Success = true;
            else FinalizeError(res, null);
        }
        return res;
    }

    // ---------- 列表（同步，供 UI 预扫描/浏览） ----------
    public List<EntryInfo> TryList(string archive, string? password)
    {
        try
        {
            var args = new List<string> { "l", "-slt", "-ba", archive };
            if (!string.IsNullOrEmpty(password)) args.Add($"-p{password}");
            using var p = Process.Start(NewPsi(args, true))!;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            return ParseListing(Redact(output, password));
        }
        catch { return new(); }
    }

    private static List<EntryInfo> ParseListing(string output)
    {
        var list = new List<EntryInfo>();
        string? path = null; long size = 0; bool dir = false; bool have = false;
        foreach (var raw in output.Replace("\r", "").Split('\n'))
        {
            var line = raw;
            var m = Regex.Match(line, @"^\s*(Path|Folder)\s*=\s*(.*)$");
            if (m.Success)
            {
                if (have) list.Add(new EntryInfo(path!, size, dir));
                path = m.Groups[2].Value.Trim();
                dir = m.Groups[1].Value == "Folder";
                size = 0; have = true;
                continue;
            }
            var ms = Regex.Match(line, @"^\s*Size\s*=\s*(\d+)");
            if (ms.Success && have) size = long.Parse(ms.Groups[1].Value);
        }
        if (have) list.Add(new EntryInfo(path!, size, dir));
        return list;
    }

    private static void PreRenameConflicts(string dest, List<EntryInfo> entries)
    {
        try
        {
            foreach (var e in entries)
            {
                if (e.IsDir || PathGuard.IsUnsafe(e.Path)) continue;
                var target = Path.Combine(dest, e.Path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(target))
                {
                    var dir = Path.GetDirectoryName(target)!;
                    var name = Path.GetFileNameWithoutExtension(target);
                    var ext = Path.GetExtension(target);
                    int n = 1; string np;
                    do { np = Path.Combine(dir, $"{name} ({n++}){ext}"); } while (File.Exists(np));
                    File.Move(target, np);
                }
            }
        }
        catch { /* 尽力而为 */ }
    }

    // ---------- 进程运行核心 ----------
    private async Task<TaskResult> RunAsync(List<string> args, string? password,
        ProgressHandler? progress, CancellationToken ct)
    {
        var res = new TaskResult();
        res.Log.Add("命令：7z " + string.Join(" ", args.Select(a => Redact(a, password) is var r ? R(r) : r)));

        ProcessStartInfo psi;
        try { psi = NewPsi(args, true); }
        catch (Exception ex) { res.ErrorKind = "EngineMissing"; res.ErrorMessage = "无法启动压缩引擎：" + ex.Message; return res; }

        Process proc;
        try { proc = Process.Start(psi)!; }
        catch (Exception ex)
        {
            res.ErrorKind = "EngineMissing";
            res.ErrorMessage = "无法启动压缩引擎（7z.exe）。请检查依赖或重新安装。原始错误：" + ex.Message;
            return res;
        }

        using (proc)
        {
            using var reg = ct.Register(() => { try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { } });

            var err = new StringBuilder();
            var outp = new StringBuilder();
            var so = PumpAsync(proc.StandardOutput, err, outp, isErr: false, progress, password);
            var se = PumpAsync(proc.StandardError, err, outp, isErr: true, progress, password);
            await Task.WhenAll(so, se).ConfigureAwait(false);

            try { await proc.WaitForExitAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { }

            res.Cancelled = ct.IsCancellationRequested;
            res.ExitCode = proc.HasExited ? SafeExit(proc) : -1;
            res.Output = Redact(err.ToString() + Environment.NewLine + outp.ToString(), password);

            if (res.Cancelled)
            {
                res.ErrorKind = "Cancelled";
                res.ErrorMessage = "任务已取消，未完成的结果不应视为成功输出。";
            }
            else if (res.ExitCode != 0)
            {
                FinalizeError(res, null);
            }
        }
        return res;
    }

    private static int SafeExit(Process p) { try { return p.ExitCode; } catch { return -1; } }
    private static string R(string s) => s;

    private ProcessStartInfo NewPsi(List<string> args, bool redirect)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _sevenZip,
            WorkingDirectory = BaseDir(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    private static async Task PumpAsync(StreamReader reader, StringBuilder err,
        StringBuilder outp, bool isErr, ProgressHandler? progress, string? password)
    {
        var buf = new char[4096];
        var sb = new StringBuilder();
        while (true)
        {
            int n;
            try { n = await reader.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false); }
            catch { break; }
            if (n <= 0) break;
            for (int i = 0; i < n; i++)
            {
                char c = buf[i];
                if (c == '\n' || c == '\r')
                {
                    if (sb.Length > 0)
                    {
                        string line = sb.ToString(); sb.Clear();
                        Emit(Redact(line, password), isErr, err, outp, progress);
                    }
                }
                else sb.Append(c);
            }
        }
        if (sb.Length > 0) Emit(Redact(sb.ToString(), password), isErr, err, outp, progress);
    }

    private static void Emit(string line, bool isErr, StringBuilder err,
        StringBuilder outp, ProgressHandler? progress)
    {
        var t = line.Trim();
        if (t.Length == 0) return;
        (isErr ? err : outp).AppendLine(t);
        if (progress == null) return;

        var m = Regex.Match(t, @"(\d{1,3})%");
        if (m.Success)
        {
            if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p)
                && p >= 0 && p <= 100)
                progress(p, null);
        }
        var fm = Regex.Match(t, @"^(Extracting|Compressing|Scanning|Cleaning)\s+(.+)$", RegexOptions.IgnoreCase);
        if (fm.Success) progress(null, fm.Groups[2].Value.Trim());
    }

    // ---------- 错误分类 ----------
    private static void FinalizeError(TaskResult r, string? defaultKind)
    {
        var low = (r.Output ?? "").ToLowerInvariant();
        string kind, msg;

        if (defaultKind == "Cancelled" || r.Cancelled)
        { kind = "Cancelled"; msg = "任务已取消。"; }
        else if (low.Contains("wrong password") || low.Contains("data error in encrypted")
                 || low.Contains("cannot open encrypted") || low.Contains("headers error")
                 || low.Contains("encrypted archive") || low.Contains("password"))
        { kind = "WrongPassword"; msg = "密码错误或压缩包已加密。请重新输入正确密码后重试。"; }
        else if (low.Contains("not enough space") || low.Contains("磁盘空间不足"))
        { kind = "NoSpace"; msg = "目标磁盘空间不足，请清理空间或选择其他保存位置。"; }
        else if (low.Contains("access is denied") || low.Contains("拒绝访问")
                 || low.Contains("being used by another process") || low.Contains("used by another")
                 || low.Contains("process cannot access"))
        { kind = "AccessDenied"; msg = "权限不足或目标文件正被占用，请关闭占用程序或换到可写目录后重试。"; }
        else if (low.Contains("cannot open the file as archive") || low.Contains("is not archive type")
                 || low.Contains("unsupported method") || low.Contains("not supported")
                 || low.Contains("unknown format"))
        { kind = "Unsupported"; msg = "压缩包格式不支持或无法识别。"; }
        else if (low.Contains("data error") || low.Contains("unexpected end of archive")
                 || low.Contains("corrupt") || low.Contains("broken"))
        { kind = "Corrupt"; msg = "压缩包数据损坏或校验失败。"; }
        else if (defaultKind != null)
        { kind = defaultKind; msg = "操作失败，请查看详细信息。"; }
        else
        { kind = "Unknown"; msg = $"操作失败（引擎返回码 {r.ExitCode}），请查看详细信息。"; }

        r.ErrorKind = kind;
        r.ErrorMessage = msg;
        r.Success = false;
    }

    // ---------- 脱敏 ----------
    private static string Redact(string s, string? password)
    {
        if (string.IsNullOrEmpty(password)) return s;
        return s.Replace("-p" + password, "-p******").Replace(password, "******");
    }

    private static string FormatToken(ArchiveFormat f) =>
        f == ArchiveFormat.SevenZip ? "7z" : "zip";

    private static string LevelDigit(CompressionLevel l) => l switch
    {
        CompressionLevel.Fast => "1",
        CompressionLevel.Maximum => "9",
        _ => "5"
    };

    private static string BaseDir() =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
}
