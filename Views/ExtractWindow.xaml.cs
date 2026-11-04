using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SimpleCompress.Models;
using SimpleCompress.Services;

namespace SimpleCompress.Views;

public partial class ExtractWindow : Window
{
    private string[] _files;
    private readonly EngineLocator.EngineInfo _engine;
    private readonly AppSettings _s;

    public ExtractWindow(string[] files, EngineLocator.EngineInfo engine, AppSettings s, string verb)
    {
        InitializeComponent();
        _files = files;
        _engine = engine;
        _s = s;

        OpenAfter.IsChecked = s.OpenFolderAfterExtract;
        ConflictBox.SelectedItem = s.DefaultConflict switch
        {
            ConflictPolicy.Skip => ConflictBox.Items[2],
            ConflictPolicy.Overwrite => ConflictBox.Items[3],
            ConflictPolicy.Rename => ConflictBox.Items[1],
            _ => ConflictBox.Items[0]
        };

        Loaded += (_, _) => Refresh(files, verb);
    }

    private void Refresh(string[] files, string verb)
    {
        _files = files;
        var first = files[0];
        var stem = StemOf(first);
        SummaryName.Text = Path.GetFileName(first);
        SummaryCount.Text = files.Length > 1 ? $"等 {files.Length} 个压缩包（将分别解压到各自同名文件夹）" : "1 个压缩包";

        var dir = Path.GetDirectoryName(first) ?? AppContext.BaseDirectory;
        if (verb == ShellIntegration.CmdExtractHere) DestBox.Text = dir;
        else DestBox.Text = Path.Combine(dir, stem);   // 同名文件夹
    }

    private static string StemOf(string archive)
    {
        var name = Path.GetFileName(archive);
        return System.Text.RegularExpressions.Regex.Replace(name, @"\.(zip|7z|rar|tar|gz|bz2|xz|iso|zst|cab)$", "", RegexOptions.IgnoreCase);
    }

    private void Reselect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择压缩包",
            Multiselect = true,
            Filter = "压缩包|*.zip;*.7z;*.rar;*.tar;*.gz;*.bz2;*.xz;*.iso;*.zst;*.cab|所有文件|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;
        Refresh(dlg.FileNames, ShellIntegration.CmdExtractDialog);
    }

    private void BrowseDest_Click(object sender, RoutedEventArgs e)
    {
        var fd = new OpenFolderDialog { Title = "选择解压位置" };
        if (Directory.Exists(DestBox.Text)) fd.InitialDirectory = DestBox.Text;
        if (fd.ShowDialog(this) == true) DestBox.Text = fd.FolderName;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        var dest = DestBox.Text.Trim();
        if (dest.Length == 0) { Warn("请选择解压位置。"); return; }

        var tag = ((ComboBoxItem)ConflictBox.SelectedItem).Tag as string;
        var policy = tag switch
        {
            "rename" => ConflictPolicy.Rename,
            "skip" => ConflictPolicy.Skip,
            "overwrite" => ConflictPolicy.Overwrite,
            _ => ConflictPolicy.Ask
        };

        // 逐个压缩包：分别解压到各自目标（同名文件夹策略时，多包用各自目录）
        foreach (var archive in _files)
            ExtractOne(archive, dest, policy);

        DialogResult = true;
    }

    private void ExtractOne(string archive, string destBox, ConflictPolicy policy)
    {
        string destination;
        bool multiple = _files.Length > 1;
        var dirOfArchive = Path.GetDirectoryName(archive) ?? AppContext.BaseDirectory;

        if (multiple)
        {
            // 多包：各自来源目录下的同名文件夹
            destination = Path.Combine(dirOfArchive, StemOf(archive));
        }
        else
        {
            destination = destBox;
        }

        // 询问同名：预扫描冲突，逐项确认，支持应用到全部
        var effective = policy;
        if (policy == ConflictPolicy.Ask)
        {
            effective = ResolveAsk(archive, destination);
            if (effective == ConflictPolicy.Ask) effective = ConflictPolicy.Overwrite; // 无冲突
        }

        Directory.CreateDirectory(destination);
        if (!PathGuard.IsWritableDirectory(destination))
        {
            Warn($"目标目录不可写：{destination}");
            return;
        }

        var req = new ExtractRequest
        {
            Archive = archive,
            Destination = destination,
            Conflict = effective
        };
        TaskRunner.RunExtract(this, req, _engine);
    }

    private ConflictPolicy ResolveAsk(string archive, string destination)
    {
        var eng = new ArchiveEngine(_engine.Path7z);
        var entries = eng.TryList(archive, null).Where(x => !x.IsDir && !PathGuard.IsUnsafe(x.Path)).ToList();
        var conflicts = entries
            .Select(x => Path.Combine(destination, x.Path.Replace('/', Path.DirectorySeparatorChar)))
            .Where(File.Exists)
            .ToList();

        if (conflicts.Count == 0) return ConflictPolicy.Ask;

        ConflictPolicy global = ConflictPolicy.Overwrite;
        bool applyAll = false;
        foreach (var c in conflicts)
        {
            if (applyAll) break;
            var res = MessageBox.Show(this,
                $"目标已存在同名文件：\n{Path.GetFileName(c)}\n\n是=替换  否=跳过  取消=保留两份",
                "同名文件冲突", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            global = res switch
            {
                MessageBoxResult.Yes => ConflictPolicy.Overwrite,
                MessageBoxResult.No => ConflictPolicy.Skip,
                _ => ConflictPolicy.Rename
            };
            var again = MessageBox.Show(this, "应用到全部冲突项？", "小压匠",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (again == MessageBoxResult.Yes) applyAll = true;
        }
        return global;
    }

    // ---------- 直接操作（右键：解压到同名 / 当前 / 分别解压） ----------
    public static void RunDirect(Window? owner, string[] archives, EngineLocator.EngineInfo engine,
        AppSettings s, string verb)
    {
        foreach (var archive in archives)
        {
            var dir = Path.GetDirectoryName(archive) ?? AppContext.BaseDirectory;
            string destination = verb switch
            {
                ShellIntegration.CmdExtractHere => dir,
                _ => Path.Combine(dir, StemOf(archive))   // extract-quick / extract-each
            };
            try { Directory.CreateDirectory(destination); } catch { }

            var req = new ExtractRequest
            {
                Archive = archive,
                Destination = destination,
                Conflict = ConflictPolicy.Overwrite
            };
            TaskRunner.RunExtract(owner, req, engine);
            if (s.OpenFolderAfterExtract) { /* TaskRunner 成功已打开 */ }
        }
    }

    private void Warn(string m) =>
        MessageBox.Show(this, m, "小压匠", MessageBoxButton.OK, MessageBoxImage.Warning);
}
