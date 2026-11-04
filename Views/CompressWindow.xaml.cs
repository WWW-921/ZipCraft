using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SimpleCompress.Models;
using SimpleCompress.Services;

namespace SimpleCompress.Views;

public partial class CompressWindow : Window
{
    private string[] _inputs;
    private readonly EngineLocator.EngineInfo _engine;
    private readonly AppSettings _s;
    private bool _userEditedName;

    public CompressWindow(string[] files, EngineLocator.EngineInfo engine, AppSettings s)
    {
        InitializeComponent();
        _inputs = files;
        _engine = engine;
        _s = s;

        // 应用默认格式
        if (s.DefaultFormat == ArchiveFormat.SevenZip)
            FormatBox.SelectedIndex = 1;
        LevelBox.SelectedItem = s.DefaultLevel switch
        {
            CompressionLevel.Fast => LevelBox.Items[0],
            CompressionLevel.Maximum => LevelBox.Items[2],
            _ => LevelBox.Items[1]
        };

        Loaded += (_, _) => RefreshInputs(files, applyDefaultName: true);
        NameBox.LostFocus += (_, _) => { if (NameBox.Text.Trim().Length > 0) _userEditedName = true; };
    }

    private void RefreshInputs(string[] files, bool applyDefaultName)
    {
        _inputs = files;
        var first = files[0];
        var firstName = Path.GetFileName(first.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(firstName)) firstName = Path.GetPathRoot(first) ?? "压缩包";

        SummaryName.Text = firstName;
        SummaryCount.Text = files.Length > 1 ? $"等 {files.Length} 个项目" : "1 个项目";

        if (applyDefaultName || !_userEditedName)
        {
            var stem = Path.GetFileNameWithoutExtension(firstName);
            if (string.IsNullOrEmpty(stem)) stem = firstName;   // 文件夹无扩展名时
            NameBox.Text = stem + "." + CurrentExt();
        }

        // 保存位置：默认取首个项目的父目录
        var parent = Directory.Exists(first)
            ? Path.GetDirectoryName(first.TrimEnd(Path.DirectorySeparatorChar)) ?? first
            : Path.GetDirectoryName(first) ?? AppContext.BaseDirectory;
        DestBox.Text = parent;
    }

    private string CurrentExt() =>
        ((ComboBoxItem)FormatBox.SelectedItem).Tag as string == "7z" ? "7z" : "zip";

    private void Format_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        // 同步扩展名，避免重复追加
        var ext = "." + CurrentExt();
        var name = NameBox.Text.Trim();
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\.(zip|7z)$", "", RegexOptions.IgnoreCase);
        NameBox.Text = name + ext;
        EncHeader.IsEnabled = CurrentExt() == "7z";
        if (CurrentExt() != "7z") EncHeader.IsChecked = false;
    }

    private void Reselect_Click(object sender, RoutedEventArgs e)
    {
        var picked = new OpenFolderOrFilesDialog().PickFiles(this, multiselect: true);
        if (picked.Count == 0) return;
        _userEditedName = false;
        RefreshInputs(picked.ToArray(), applyDefaultName: true);
    }

    private void BrowseDest_Click(object sender, RoutedEventArgs e)
    {
        var fd = new OpenFolderDialog { Title = "选择保存位置", InitialDirectory = SafeDir(DestBox.Text) };
        if (fd.ShowDialog(this) == true) DestBox.Text = fd.FolderName;
    }

    private static string? SafeDir(string p)
    {
        try { return Directory.Exists(p) ? p : Path.GetDirectoryName(p); } catch { return null; }
    }

    private void Pw_Toggle(object sender, RoutedEventArgs e)
    {
        PwArea.Visibility = PwToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PwShow_Toggle(object sender, RoutedEventArgs e)
    {
        if (PwShow.IsChecked == true) { PwText.Text = PwBox.Password; PwBox.Visibility = Visibility.Collapsed; PwText.Visibility = Visibility.Visible; }
        else { PwBox.Password = PwText.Text; PwText.Visibility = Visibility.Collapsed; PwBox.Visibility = Visibility.Visible; }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        // 校验
        var name = NameBox.Text.Trim();
        if (name.Length == 0) { Warn("请填写压缩包名称。"); NameBox.Focus(); return; }

        var dest = DestBox.Text.Trim();
        if (dest.Length == 0 || !Directory.Exists(dest)) { Warn("保存位置不存在，请重新选择。"); return; }
        if (!PathGuard.IsWritableDirectory(dest)) { Warn("保存位置不可写，请检查权限或换目录。"); return; }

        string? password = null;
        if (PwToggle.IsChecked == true)
        {
            password = PwShow.IsChecked == true ? PwText.Text : PwBox.Password;
            if (string.IsNullOrEmpty(password)) { Warn("已开启密码，请填写密码。"); return; }
        }

        // 确保扩展名与格式一致
        var ext = "." + CurrentExt();
        if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\.(zip|7z)$", "", RegexOptions.IgnoreCase) + ext;

        var outPath = Path.Combine(dest, name);
        if (File.Exists(outPath))
        {
            var r = MessageBox.Show(this, $"“{name}” 已存在，是否覆盖？", "同名文件",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            try { File.Delete(outPath); } catch (Exception ex) { Warn("无法覆盖已有文件：" + ex.Message); return; }
        }

        var levelTag = ((ComboBoxItem)LevelBox.SelectedItem).Tag as string;
        var level = levelTag switch
        {
            "fast" => CompressionLevel.Fast,
            "max" => CompressionLevel.Maximum,
            _ => CompressionLevel.Normal
        };
        var vol = ((ComboBoxItem)VolumeBox.SelectedItem).Tag as string;

        // 分卷对 ZIP 也支持（7z 引擎实现），等级/分卷均按引擎实际能力传参
        var req = new CompressRequest
        {
            Inputs = _inputs.ToList(),
            OutputArchive = outPath,
            Format = CurrentExt() == "7z" ? ArchiveFormat.SevenZip : ArchiveFormat.Zip,
            Level = level,
            Password = password,
            EncryptHeaders = EncHeader.IsChecked == true,
            VolumeSize = vol
        };

        TaskRunner.RunCompress(this, req, _engine);
        if (!File.Exists(outPath)) return;   // 未完成则留在界面
        DialogResult = true;
    }

    private void Warn(string m) =>
        MessageBox.Show(this, m, "小压匠", MessageBoxButton.OK, MessageBoxImage.Warning);
}
