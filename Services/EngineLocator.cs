using System.IO;
using Microsoft.Win32;

namespace SimpleCompress.Services;

/// <summary>
/// 压缩引擎（7z.exe）定位。按优先级：用户配置 → 随包 tools → 已知 PeaZip 目录 → 系统 7-Zip。
/// 不写死单一绝对路径。
/// </summary>
public static class EngineLocator
{
    public record EngineInfo(string Path7z, string Source);

    private static string BaseDir =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>返回可用的 7z.exe 信息；找不到返回 null。</summary>
    public static EngineInfo? Locate(string? configuredPath)
    {
        foreach (var c in Candidates(configuredPath))
        {
            if (File.Exists(c.Path7z)) return c;
        }
        return null;
    }

    private static IEnumerable<EngineInfo> Candidates(string? configured)
    {
        // 1) 用户显式配置
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (Path.GetFileName(configured).Equals("7z.exe", StringComparison.OrdinalIgnoreCase))
                yield return new EngineInfo(configured!, "用户配置");
            else
            {
                // 允许填目录或 peazip.exe
                var dir = TryDerive7zDirFromPeaZip(configured!);
                if (dir != null)
                    yield return new EngineInfo(dir, "用户配置(PeaZip)");
            }
        }

        // 2) 随包 tools\7z.exe
        yield return new EngineInfo(
            Path.Combine(BaseDir, "tools", "7z", "7z.exe"), "随包 tools");
        yield return new EngineInfo(
            Path.Combine(BaseDir, "tools", "7z.exe"), "随包 tools");

        // 3) 原始部署线索目录（存在才用，不假设一定存在）
        yield return new EngineInfo(
            @"D:\Tools\PeaZipPortable\peazip_portable-11.4.0.WIN64\res\bin\7z\7z.exe",
            "已知 PeaZip 目录");

        // 4) 常见 7-Zip 安装位置
        foreach (var pf in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                   Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            if (!string.IsNullOrEmpty(pf))
                yield return new EngineInfo(Path.Combine(pf, "7-Zip", "7z.exe"), "系统 7-Zip");
        }

        // 5) 注册表（7-Zip 安装项）
        var reg = ReadFromRegistry();
        if (reg != null) yield return new EngineInfo(reg, "注册表 7-Zip");
    }

    /// <summary>从 peazip.exe 或某目录推导同包内 res\bin\7z\7z.exe。</summary>
    private static string? TryDerive7zDirFromPeaZip(string p)
    {
        try
        {
            string root = Directory.Exists(p) ? p : Path.GetDirectoryName(p) ?? p;
            // peazip.exe 通常在根目录，7z 在 res\bin\7z
            var guess = Path.Combine(root, "res", "bin", "7z", "7z.exe");
            if (File.Exists(guess)) return guess;
            var guess2 = Path.Combine(root, "7z", "7z.exe");
            if (File.Exists(guess2)) return guess2;
            if (Directory.Exists(root))
            {
                var found = Directory.GetFiles(root, "7z.exe", SearchOption.AllDirectories)
                                     .FirstOrDefault();
                if (found != null) return found;
            }
        }
        catch { }
        return null;
    }

    private static string? ReadFromRegistry()
    {
        try
        {
            using var rk = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\7zFM.exe");
            var v = rk?.GetValue("") as string;
            if (!string.IsNullOrEmpty(v))
            {
                var dir = Path.GetDirectoryName(v);
                var exe = dir == null ? null : Path.Combine(dir, "7z.exe");
                if (exe != null && File.Exists(exe)) return exe;
            }
        }
        catch { }
        return null;
    }

    /// <summary>发现 PeaZip 完整界面（可选，用于“更多功能”入口）。</summary>
    public static string? LocatePeaZip(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;
        string root = BaseDir;
        var local = Path.Combine(root, "tools", "peazip.exe");
        if (File.Exists(local)) return local;
        var known = @"D:\Tools\PeaZipPortable\peazip_portable-11.4.0.WIN64\peazip.exe";
        if (File.Exists(known)) return known;
        return null;
    }
}
