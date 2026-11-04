using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SimpleCompress.Views;

/// <summary>压缩项目选择：支持多选文件与选择文件夹，两者可组合。</summary>
public class OpenFolderOrFilesDialog
{
    public List<string> PickFiles(Window owner, bool multiselect)
    {
        var result = new List<string>();

        var win = new Window
        {
            Title = "选择要压缩的项目",
            Owner = owner,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.White
        };

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = "选择文件或文件夹（可分别添加，组合多个项目）",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var list = new TextBlock { Text = "尚未选择", FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };
        var chosen = new List<string>();

        var bFile = new Button { Content = "选择文件…", Style = (Style)Application.Current.Resources["PlainButton"], Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
        var bDir = new Button { Content = "选择文件夹…", Style = (Style)Application.Current.Resources["PlainButton"], Margin = new Thickness(0, 0, 0, 16), HorizontalAlignment = HorizontalAlignment.Stretch };

        bFile.Click += (_, _) =>
        {
            var ofd = new OpenFileDialog { Multiselect = multiselect, Title = "选择文件" };
            if (ofd.ShowDialog(win) == true)
            {
                chosen.AddRange(ofd.FileNames);
                list.Text = string.Join("\n", chosen.Select(System.IO.Path.GetFileName));
            }
        };
        bDir.Click += (_, _) =>
        {
            var fd = new OpenFolderDialog { Title = "选择文件夹" };
            if (fd.ShowDialog(win) == true)
            {
                chosen.Add(fd.FolderName);
                list.Text = string.Join("\n", chosen.Select(System.IO.Path.GetFileName));
            }
        };

        panel.Children.Add(bFile);
        panel.Children.Add(bDir);
        panel.Children.Add(list);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "取消", Width = 80, Height = 32, Margin = new Thickness(0, 0, 10, 0), Style = (Style)Application.Current.Resources["PlainButton"] };
        var ok = new Button { Content = "下一步", Width = 92, Height = 32, Style = (Style)Application.Current.Resources["PrimaryButton"] };
        cancel.Click += (_, _) => win.DialogResult = false;
        ok.Click += (_, _) => win.DialogResult = true;
        btns.Children.Add(cancel); btns.Children.Add(ok);
        panel.Children.Add(btns);

        win.Content = panel;
        if (win.ShowDialog() == true && chosen.Count > 0)
            result.AddRange(chosen);
        return result;
    }
}
