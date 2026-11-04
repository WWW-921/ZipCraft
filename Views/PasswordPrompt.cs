using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SimpleCompress.Views;

/// <summary>简单的密码输入对话框（不记录到日志）。</summary>
public static class PasswordPrompt
{
    public static string? Ask(Window owner, string archiveName)
    {
        var win = new Window
        {
            Title = "需要密码",
            Owner = owner,
            Width = 380,
            Height = 190,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.White
        };

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = "“" + Path.GetFileNameWithoutExtension(archiveName) + "” 已加密，请输入密码：",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            FontSize = 13
        });

        var pw = new PasswordBox { FontSize = 14, Padding = new Thickness(8, 6, 8, 6) };
        panel.Children.Add(pw);

        var toggle = new CheckBox { Content = "显示密码", Margin = new Thickness(0, 10, 0, 0), FontSize = 13 };
        var textPw = new TextBox { FontSize = 14, Padding = new Thickness(8, 6, 8, 6), Visibility = Visibility.Collapsed };
        panel.Children.Add(toggle);
        panel.Children.Add(textPw);
        toggle.Checked += (_, _) => { textPw.Text = pw.Password; pw.Visibility = Visibility.Collapsed; textPw.Visibility = Visibility.Visible; };
        toggle.Unchecked += (_, _) => { pw.Password = textPw.Text; textPw.Visibility = Visibility.Collapsed; pw.Visibility = Visibility.Visible; };

        var btns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancel = new Button { Content = "取消", Width = 84, Height = 32, Margin = new Thickness(0, 0, 10, 0) };
        var ok = new Button { Content = "确定", Width = 84, Height = 32 };
        cancel.Click += (_, _) => { win.DialogResult = false; };
        ok.Click += (_, _) => { win.DialogResult = true; };
        btns.Children.Add(cancel); btns.Children.Add(ok);
        panel.Children.Add(btns);

        win.Content = panel;
        var res = win.ShowDialog();
        if (res == true)
        {
            var value = toggle.IsChecked == true ? textPw.Text : pw.Password;
            return value.Length == 0 ? "" : value;
        }
        return null;
    }
}
