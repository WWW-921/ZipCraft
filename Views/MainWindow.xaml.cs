using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using SimpleCompress.Models;
using SimpleCompress.Services;

namespace SimpleCompress.Views;

public partial class MainWindow : Window
{
    private static readonly string[] ArchiveExt =
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".iso", ".zst", ".cab" };

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshEngine();
    }

    private AppSettings S => SettingsStore.Load();

    private void RefreshEngine()
    {
        var eng = EngineLocator.Locate(S.EnginePath);
        EngineWarn.Visibility = eng == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private EngineLocator.EngineInfo? NeedEngine()
    {
        var eng = EngineLocator.Locate(S.EnginePath);
        if (eng == null)
        {
            MessageBox.Show(this,
                "未找到压缩引擎（7z.exe）。\n\n请把随包 tools\\7z 文件夹放到程序目录，或在“设置”中指定引擎或 PeaZip 路径。",
                "缺少依赖", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        return eng;
    }

    // ---------- 压缩入口 ----------
    private void Compress_Click(object sender, RoutedEventArgs e)
    {
        if (NeedEngine() == null) return;
        var dlg = new OpenFolderOrFilesDialog();
        var files = dlg.PickFiles(this, multiselect: true);
        if (files.Count == 0) return;
        OpenCompress(files);
    }

    private void OpenCompress(List<string> files)
    {
        var eng = NeedEngine(); if (eng == null) return;
        new CompressWindow(files.ToArray(), eng, S) { Owner = this }.ShowDialog();
        RefreshEngine();
    }

    // ---------- 解压入口 ----------
    private void Extract_Click(object sender, RoutedEventArgs e)
    {
        if (NeedEngine() == null) return;
        var dlg = new OpenFileDialog
        {
            Title = "选择压缩包",
            Filter = "压缩包|" + string.Join(";", ArchiveExt.Select(x => "*" + x)) + "|所有文件|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != true) return;
        OpenExtract(dlg.FileNames, ShellIntegration.CmdExtractDialog);
    }

    private void OpenArchive_Click(object sender, RoutedEventArgs e) => Extract_Click(sender, e);

    private void OpenExtract(string[] files, string verb)
    {
        var eng = NeedEngine(); if (eng == null) return;
        new ExtractWindow(files, eng, S, verb) { Owner = this }.ShowDialog();
    }

    // ---------- 更多 / 设置 ----------
    private void More_Click(object sender, RoutedEventArgs e)
    {
        new MoreWindow(S) { Owner = this }.ShowDialog();
        RefreshEngine();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow(S) { Owner = this }.ShowDialog();
        RefreshEngine();
    }

    // ---------- 拖放分类 ----------
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.None; return; }
        e.Effects = DragDropEffects.Copy;
        DropZone.Tag = "over";
    }

    private void Window_DragLeave(object sender, DragEventArgs e) => DropZone.Tag = null;

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropZone.Tag = null;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var items = (string[])e.Data.GetData(DataFormats.FileDrop);
        ClassifyAndGo(items.ToList());
    }

    private void ClassifyAndGo(List<string> items)
    {
        if (items.Count == 0) return;
        bool allArchives = items.All(IsArchive);
        if (allArchives)
        {
            // 单个或批量压缩包 → 解压（批量分别解压到同名文件夹）
            OpenExtract(items.ToArray(),
                items.Count == 1 ? ShellIntegration.CmdExtractDialog : "extract-each");
        }
        else
        {
            // 含普通文件/文件夹（或混合）→ 压缩，并展示输入项目供确认
            var archives = items.FindAll(IsArchive);
            if (archives.Count > 0 && archives.Count < items.Count)
            {
                var r = MessageBox.Show(this,
                    "拖入的项目同时包含压缩包和普通文件。将按“压缩”处理，是否继续？",
                    "确认操作", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }
            OpenCompress(items);
        }
    }

    private static bool IsArchive(string p)
    {
        try
        {
            if (Directory.Exists(p)) return false;
            return ArchiveExt.Contains(Path.GetExtension(p).ToLowerInvariant());
        }
        catch { return false; }
    }
}
