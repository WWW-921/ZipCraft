using System.IO;
using System.Windows;
using SimpleCompress.Models;
using SimpleCompress.Services;
using SimpleCompress.Views;

namespace SimpleCompress;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = SettingsStore.Load();
        var engine = EngineLocator.Locate(settings.EnginePath);

        // 解析命令行：--cmd <verb> 后跟若干文件路径（来自右键菜单）
        var (verb, files) = ParseArgs(e.Args);

        if (verb != null)
        {
            // 来自右键菜单的直接操作
            if (engine == null) { ShowNoEngine(); return; }
            HandleShellVerb(verb, files, settings, engine);
            Shutdown();
            return;
        }

        // 普通启动：显示主界面
        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    private static (string? verb, string[] files) ParseArgs(string[] args)
    {
        string? verb = null;
        var files = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--cmd" && i + 1 < args.Length) { verb = args[++i]; continue; }
            if (args[i].StartsWith("--")) continue;
            // Explorer 有时传入未加引号的多个路径合并；做一次拆分容错
            foreach (var p in SplitPaths(args[i]))
                if (!string.IsNullOrWhiteSpace(p)) files.Add(p);
        }
        files = files.Where(FileOrDirExists).Distinct().ToList();
        return (verb, files.ToArray());
    }

    private static bool FileOrDirExists(string p)
    {
        try { return File.Exists(p) || Directory.Exists(p); } catch { return false; }
    }

    /// <summary>处理单条参数可能粘连多路径的情况（%U 展开）。</summary>
    private static IEnumerable<string> SplitPaths(string s)
    {
        // 若含引号包裹的多段
        var parts = System.Text.RegularExpressions.Regex.Matches(s, "\"([^\"]+)\"");
        if (parts.Count > 0)
        {
            foreach (System.Text.RegularExpressions.Match m in parts)
                yield return m.Groups[1].Value;
            yield break;
        }
        yield return s;
    }

    private void HandleShellVerb(string verb, string[] files, AppSettings s, EngineLocator.EngineInfo engine)
    {
        if (files.Length == 0) return;
        switch (verb)
        {
            case ShellIntegration.CmdCompressQuick:
                QuickCompress(files, engine, s);
                break;
            case ShellIntegration.CmdCompressDialog:
                ShowCompressDialog(files, engine, s);
                break;
            case ShellIntegration.CmdExtractQuick:
            case ShellIntegration.CmdExtractHere:
                ExtractWindow.RunDirect(null, files, engine, s, verb);
                break;
            case ShellIntegration.CmdExtractDialog:
                ShowExtractDialog(files, engine, s, verb);
                break;
            case ShellIntegration.CmdBrowse:
                new BrowseWindow(files[0], engine).ShowDialog();
                break;
        }
    }

    private void QuickCompress(string[] files, EngineLocator.EngineInfo engine, AppSettings s)
    {
        // 在源文件位置直接生成 “名称.zip”，标准等级
        var first = files[0];
        var dir = Directory.Exists(first) ? first : Path.GetDirectoryName(first) ?? Base();
        var baseName = Path.GetFileName(first.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(baseName)) baseName = "压缩包";
        // 同名 zip 若存在则加序号
        var outPath = UniquePath(Path.Combine(dir, baseName + ".zip"));

        var req = new CompressRequest
        {
            Inputs = files.ToList(),
            OutputArchive = outPath,
            Format = ArchiveFormat.Zip,
            Level = s.DefaultLevel
        };
        TaskRunner.RunCompress(null, req, engine);
    }

    private void ShowCompressDialog(string[] files, EngineLocator.EngineInfo engine, AppSettings s)
    {
        new CompressWindow(files, engine, s).Show();
    }

    private void ShowExtractDialog(string[] files, EngineLocator.EngineInfo engine, AppSettings s, string verb)
    {
        new ExtractWindow(files, engine, s, verb).Show();
    }

    private void ShowNoEngine()
    {
        MessageBox.Show(
            "未找到压缩引擎（7z.exe）。\n\n请将随包 tools\\7z\\7z.exe 放到程序目录，" +
            "或在应用“设置”中指定引擎/PeaZip 路径。",
            "小压匠", MessageBoxButton.OK, MessageBoxImage.Warning);
        var main = new MainWindow();
        MainWindow = main; main.Show();
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        int n = 1; string np;
        do { np = Path.Combine(dir, $"{name} ({n++}){ext}"); } while (File.Exists(np));
        return np;
    }

    private static string Base() =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
}
