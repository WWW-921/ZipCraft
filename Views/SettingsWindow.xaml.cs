using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SimpleCompress.Models;
using SimpleCompress.Services;

namespace SimpleCompress.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _s;

    public SettingsWindow(AppSettings s)
    {
        InitializeComponent();
        _s = s;

        ShellToggle.IsChecked = ShellIntegration.IsRegistered();
        ExtractNamed.IsChecked = s.ExtractToNamedFolder;
        OpenAfterExtract.IsChecked = s.OpenFolderAfterExtract;
        EnginePathBox.Text = s.EnginePath;

        SelectByTag(DefaultFormat, s.DefaultFormat == ArchiveFormat.SevenZip ? "7z" : "zip");
        SelectByTag(DefaultLevel, s.DefaultLevel switch
        {
            CompressionLevel.Fast => "fast",
            CompressionLevel.Maximum => "max",
            _ => "normal"
        });
        SelectByTag(DefaultConflict, s.DefaultConflict switch
        {
            ConflictPolicy.Rename => "rename",
            ConflictPolicy.Skip => "skip",
            ConflictPolicy.Overwrite => "overwrite",
            _ => "ask"
        });

        RefreshEngineState();
    }

    private static void SelectByTag(ComboBox box, string tag)
    {
        foreach (ComboBoxItem it in box.Items)
            if ((it.Tag as string) == tag) { box.SelectedItem = it; return; }
    }

    private static string TagOf(ComboBox box) => ((ComboBoxItem)box.SelectedItem).Tag as string ?? "";

    private void RefreshEngineState()
    {
        var eng = EngineLocator.Locate(TagOfDefaultOrText());
        EngineState.Text = eng == null
            ? "当前状态：未找到 7z.exe。请浏览选择，或把随包 tools\\7z 放到程序目录。"
            : $"当前状态：已找到引擎（{eng.Source}）\n{eng.Path7z}";
    }

    private string TagOfDefaultOrText()
    {
        var t = EnginePathBox.Text.Trim();
        return t.Length == 0 ? _s.EnginePath : t;
    }

    private void BrowseEngine_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog
        {
            Title = "选择 7z.exe",
            Filter = "7z 引擎|7z.exe|可执行文件|*.exe",
            CheckFileExists = true
        };
        if (d.ShowDialog(this) == true) { EnginePathBox.Text = d.FileName; RefreshEngineState(); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsStore.Load();
        s.DefaultFormat = TagOf(DefaultFormat) == "7z" ? ArchiveFormat.SevenZip : ArchiveFormat.Zip;
        s.DefaultLevel = TagOf(DefaultLevel) switch
        {
            "fast" => CompressionLevel.Fast,
            "max" => CompressionLevel.Maximum,
            _ => CompressionLevel.Normal
        };
        s.DefaultConflict = TagOf(DefaultConflict) switch
        {
            "rename" => ConflictPolicy.Rename,
            "skip" => ConflictPolicy.Skip,
            "overwrite" => ConflictPolicy.Overwrite,
            _ => ConflictPolicy.Ask
        };
        s.ExtractToNamedFolder = ExtractNamed.IsChecked == true;
        s.OpenFolderAfterExtract = OpenAfterExtract.IsChecked == true;
        s.EnginePath = EnginePathBox.Text.Trim();

        // 右键菜单注册/移除
        bool want = ShellToggle.IsChecked == true;
        bool registered = ShellIntegration.IsRegistered();
        try
        {
            if (want && !registered) ShellIntegration.Register();
            else if (!want && registered) ShellIntegration.Unregister();
            // 若已启用，重新注册以刷新当前 exe 路径
            else if (want && registered) { ShellIntegration.Unregister(); ShellIntegration.Register(); }
            s.ShellMenuEnabled = want;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "更新右键菜单失败：" + ex.Message, "设置",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        SettingsStore.Save(s);
        MessageBox.Show(this, "设置已保存。", "小压匠", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
    }
}
