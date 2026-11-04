using System.Diagnostics;
using System.IO;
using System.Windows;
using SimpleCompress.Models;
using SimpleCompress.Services;

namespace SimpleCompress.Views;

/// <summary>把压缩/解压/测试请求跑起来并展示 TaskWindow。供主界面与右键命令复用。</summary>
public static class TaskRunner
{
    public static void RunCompress(Window? owner, CompressRequest req, EngineLocator.EngineInfo engine)
    {
        var eng = new ArchiveEngine(engine.Path7z);
        string subject = Path.GetFileName(req.OutputArchive);
        var win = new TaskWindow("压缩", subject, req.OutputArchive,
            (ct, prog) => eng.CompressAsync(req, prog, ct));
        if (owner != null) win.Owner = owner;
        win.ShowDialog();
        if (win.CompletedOk)
            OpenInExplorer(req.OutputArchive, select: true);
    }

    public static void RunExtract(Window? owner, ExtractRequest req, EngineLocator.EngineInfo engine)
    {
        var eng = new ArchiveEngine(engine.Path7z);
        string subject = Path.GetFileName(req.Archive);

        var win = new TaskWindow("解压", subject, req.Destination,
            (ct, prog) => eng.ExtractAsync(req, prog, ct),
            retryableArchive: req.Archive);
        if (owner != null) win.Owner = owner;
        win.ShowDialog();

        // 密码错误重试：TaskWindow 通过 RetryWithPassword 通知
        if (win.RetryWithPassword != null)
        {
            req.Password = win.RetryWithPassword;
            RunExtract(owner, req, engine);   // 递归重试
            return;
        }

        if (win.CompletedOk)
            OpenInExplorer(req.Destination, select: false);
    }

    public static void RunTest(Window? owner, string archive, EngineLocator.EngineInfo engine)
    {
        var eng = new ArchiveEngine(engine.Path7z);
        var win = new TaskWindow("测试", Path.GetFileName(archive), archive,
            (ct, prog) => eng.TestAsync(archive, null, prog, ct),
            retryableArchive: archive);
        if (owner != null) win.Owner = owner;
        win.ShowDialog();
        if (win.RetryWithPassword != null)
            RunTestWithPassword(owner, archive, win.RetryWithPassword, eng);
    }

    private static void RunTestWithPassword(Window? owner, string archive, string pw, ArchiveEngine eng)
    {
        var win = new TaskWindow("测试", Path.GetFileName(archive), archive,
            (ct, prog) => eng.TestAsync(archive, pw, prog, ct));
        if (owner != null) win.Owner = owner;
        win.ShowDialog();
    }

    public static void OpenInExplorer(string path, bool select)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            if (select && File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }
}
