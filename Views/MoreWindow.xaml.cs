using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using SimpleCompress.Models;
using SimpleCompress.Services;

namespace SimpleCompress.Views;

public partial class MoreWindow : Window
{
    private readonly AppSettings _s;
    private static readonly string Filter =
        "压缩包|*.zip;*.7z;*.rar;*.tar;*.gz;*.bz2;*.xz;*.iso;*.zst;*.cab|所有文件|*.*";

    public MoreWindow(AppSettings s)
    {
        InitializeComponent();
        _s = s;
    }

    private EngineLocator.EngineInfo? NeedEngine()
    {
        var e = EngineLocator.Locate(_s.EnginePath);
        if (e == null)
            MessageBox.Show(this, "未找到压缩引擎（7z.exe），请在“设置”中配置路径。",
                "缺少依赖", MessageBoxButton.OK, MessageBoxImage.Warning);
        return e;
    }

    private string? PickArchive()
    {
        var d = new OpenFileDialog { Title = "选择压缩包", Filter = Filter };
        return d.ShowDialog(this) == true ? d.FileName : null;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var eng = NeedEngine(); if (eng == null) return;
        var a = PickArchive(); if (a == null) return;
        new BrowseWindow(a, eng) { Owner = this }.ShowDialog();
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        var eng = NeedEngine(); if (eng == null) return;
        var a = PickArchive(); if (a == null) return;
        TaskRunner.RunTest(this, a, eng);
    }

    private void AdvCompress_Click(object sender, RoutedEventArgs e)
    {
        var eng = NeedEngine(); if (eng == null) return;
        var files = new OpenFolderOrFilesDialog().PickFiles(this, multiselect: true);
        if (files.Count == 0) return;
        new CompressWindow(files.ToArray(), eng, _s) { Owner = this }.ShowDialog();
    }

    private void PeaZip_Click(object sender, RoutedEventArgs e)
    {
        var p = EngineLocator.LocatePeaZip(_s.PeaZipPath);
        if (p == null)
        {
            MessageBox.Show(this,
                "未找到 PeaZip 完整界面。\n\n请在“设置”中指定 peazip.exe 路径，" +
                "或安装/放置 PeaZip 便携版。",
                "缺少依赖", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "启动 PeaZip 失败：" + ex.Message, "小压匠",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
