using System.IO;
using System.Windows;
using SimpleCompress.Models;
using SimpleCompress.Services;

namespace SimpleCompress.Views;

public partial class BrowseWindow : Window
{
    private readonly string _archive;
    private readonly EngineLocator.EngineInfo _engine;

    public record Row(string Icon, string Name, string Size, string Kind);

    public BrowseWindow(string archive, EngineLocator.EngineInfo engine)
    {
        InitializeComponent();
        _archive = archive;
        _engine = engine;
        HeadText.Text = Path.GetFileName(archive);
        Load();
    }

    private void Load()
    {
        var eng = new ArchiveEngine(_engine.Path7z);
        var entries = eng.TryList(_archive, null);
        var rows = entries
            .Where(e => !string.IsNullOrEmpty(e.Path))
            .Select(e => new Row(
                e.IsDir ? "📁" : "📄",
                e.Path,
                e.IsDir ? "" : HumanSize(e.Size),
                e.IsDir ? "文件夹" : "文件"))
            .ToList();

        List.ItemsSource = rows;
        CountText.Text = $"共 {entries.Count(x => !x.IsDir)} 个文件，{entries.Count(x => x.IsDir)} 个文件夹";
    }

    private static string HumanSize(long b) => b switch
    {
        >= 1073741824 => $"{b / 1073741824.0:F1} GB",
        >= 1048576 => $"{b / 1048576.0:F1} MB",
        >= 1024 => $"{b / 1024.0:F1} KB",
        _ => $"{b} B"
    };

    private void ExtractAll_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsStore.Load();
        var dir = Path.GetDirectoryName(_archive) ?? AppContext.BaseDirectory;
        var stem = System.Text.RegularExpressions.Regex.Replace(
            Path.GetFileName(_archive), @"\.[^.]+$", "");
        var dest = Path.Combine(dir, stem);
        Directory.CreateDirectory(dest);
        TaskRunner.RunExtract(this, new ExtractRequest
        {
            Archive = _archive,
            Destination = dest,
            Conflict = ConflictPolicy.Overwrite
        }, _engine);
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
