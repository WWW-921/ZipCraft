using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using SimpleCompress.Models;
using SimpleCompress.Services;

namespace SimpleCompress.Views;

/// <summary>通用任务窗口：真实进度、取消、结果与错误分类。支持“开始/取消/打开文件夹/重试”。</summary>
public partial class TaskWindow : Window
{
    private readonly CancellationTokenSource _cts = new();
    private readonly DispatcherTimer _pulse = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly string _opWord;              // 压缩 / 解压 / 测试
    private readonly string _subject;             // 主项目名
    private readonly Func<CancellationToken, ArchiveEngine.ProgressHandler, Task<TaskResult>> _body;
    private readonly string? _passwordRetryArchive;   // 需要密码重试时的压缩包
    private string? _lastTarget;
    private bool _started;
    private bool _indeterminate;
    public bool CompletedOk { get; private set; }

    /// <param name="retryable">若为解压/测试且需要密码，提供压缩包以便重试。</param>
    public TaskWindow(string opWord, string subject, string resultPath,
        Func<CancellationToken, ArchiveEngine.ProgressHandler, Task<TaskResult>> body,
        string? retryableArchive = null)
    {
        InitializeComponent();
        _opWord = opWord;
        _subject = subject;
        _body = body;
        _lastTarget = resultPath;
        _passwordRetryArchive = retryableArchive;
        TitleText.Text = "正在" + opWord + "文件";
        SubText.Text = subject;
        PrimaryBtn.Content = "开始" + opWord;
        _pulse.Tick += (_, _) =>
        {
            // 不确定进度：来回摆动
            if (_indeterminate) Bar.IsIndeterminate = true;
        };
        Loaded += (_, _) => Start();
    }

    private void Start()
    {
        if (_started) return;
        _started = true;
        TitleText.Text = "正在" + _opWord + "文件";
        PrimaryBtn.Visibility = Visibility.Collapsed;
        CancelBtn.Visibility = Visibility.Visible;
        ResultIconPanel.Visibility = Visibility.Collapsed;
        Bar.IsIndeterminate = true; _indeterminate = true;
        Bar.Visibility = Visibility.Visible;
        _pulse.Start();

        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        var progress = new ArchiveEngine.ProgressHandler((pct, file) =>
            Dispatcher.Invoke(() =>
            {
                if (pct is int pc)
                {
                    if (_indeterminate) { _indeterminate = false; _pulse.Stop(); Bar.IsIndeterminate = false; }
                    Bar.Value = pc; PercentText.Text = pc + "%";
                }
                if (!string.IsNullOrEmpty(file)) FileText.Text = file;
            }));

        TaskResult res;
        try { res = await _body(_cts.Token, progress); }
        catch (OperationCanceledException) { res = new TaskResult { Cancelled = true, ErrorMessage = "任务已取消。" }; }
        catch (Exception ex) { res = new TaskResult { ErrorMessage = "发生异常：" + ex.Message, ErrorKind = "Unknown" }; }

        _pulse.Stop();
        Bar.IsIndeterminate = false;
        ShowResult(res);
    }

    private void ShowResult(TaskResult res)
    {
        CancelBtn.Visibility = Visibility.Collapsed;
        Details.Visibility = Visibility.Visible;
        LogBox.Text = BuildLog(res);

        _indeterminate = false;
        Bar.IsIndeterminate = false;

        if (res.Success)
        {
            CompletedOk = true;
            Bar.Value = 100; PercentText.Text = "100%";
            FileText.Text = "已完成";
            TitleText.Text = _opWord + "完成";
            SubText.Text = "结果：" + (_lastTarget ?? "");
            ShowResultGlyph(true);
            OpenFolderBtn.Visibility = Visibility.Visible;
            PrimaryBtn.Content = "完成";
            PrimaryBtn.Visibility = Visibility.Visible;
            _doneMode = true;
        }
        else if (res.Cancelled)
        {
            Bar.Visibility = Visibility.Collapsed;
            PercentText.Text = "";
            FileText.Text = "已取消";
            TitleText.Text = _opWord + "已取消";
            SubText.Text = "任务已取消，源文件保留，未完成的结果不视为成功输出。";
            ShowResultGlyph(false);
            PrimaryBtn.Content = "重试";
            PrimaryBtn.Visibility = Visibility.Visible;
            _retryMode = true;
        }
        else
        {
            Bar.Visibility = Visibility.Collapsed;
            PercentText.Text = "";
            FileText.Text = "失败";
            TitleText.Text = _opWord + "失败";
            SubText.Text = res.ErrorMessage ?? "未知错误";
            ShowResultGlyph(false);

            if (res.ErrorKind == "WrongPassword" && _passwordRetryArchive != null)
            {
                PrimaryBtn.Content = "重新输入密码";
                PrimaryBtn.Visibility = Visibility.Visible;
                _retryPasswordMode = true;
            }
            else
            {
                PrimaryBtn.Content = "关闭";
                PrimaryBtn.Visibility = Visibility.Visible;
                _doneMode = true;
            }
        }
    }

    private bool _doneMode, _retryMode, _retryPasswordMode;

    private void ShowResultGlyph(bool ok)
    {
        ResultIconPanel.Visibility = Visibility.Visible;
        ResultIcon.Background = new System.Windows.Media.SolidColorBrush(
            ok ? System.Windows.Media.Color.FromRgb(0xe1, 0xf3, 0xeb)
               : System.Windows.Media.Color.FromRgb(0xfb, 0xe4, 0xe4));
        ResultGlyph.Foreground = new System.Windows.Media.SolidColorBrush(
            ok ? System.Windows.Media.Color.FromRgb(0x21, 0x74, 0x51)
               : System.Windows.Media.Color.FromRgb(0xb0, 0x31, 0x2e));
        ResultGlyph.Text = ok ? "✓" : "!";
    }

    private string BuildLog(TaskResult res)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var l in res.Log) sb.AppendLine(l);
        if (!string.IsNullOrEmpty(res.Output))
        {
            sb.AppendLine("—— 引擎输出 ——");
            sb.AppendLine(res.Output.Trim());
        }
        sb.AppendLine("—— 退出码：" + res.ExitCode);
        return sb.ToString();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelBtn.IsEnabled = false;
        FileText.Text = "正在取消…";
        _cts.Cancel();
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (_doneMode) { Close(); return; }
        if (_retryMode || _retryPasswordMode) { Restart(_retryPasswordMode); return; }
        Start();
    }

    private void Restart(bool askPassword)
    {
        string? newPw = null;
        if (askPassword && _passwordRetryArchive != null)
        {
            var pw = PasswordPrompt.Ask(this, _passwordRetryArchive);
            if (pw == null) return;   // 用户放弃
            newPw = pw;
        }

        // 重新构造一次 body：通过外部重新触发（简化：关闭并由调用方重跑）
        // 这里采用回调通知：设置 DialogResult 让上层决定是否重跑
        _retryWithPassword = newPw ?? "";
        DialogResult = true;   // 通知父窗口“需要重跑”
        Close();
    }

    public string? RetryWithPassword => _retryWithPassword.Length == 0 ? null : _retryWithPassword;
    private string _retryWithPassword = "";

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = _lastTarget;
        if (path == null) return;
        try
        {
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe",
                    $"/select,\"{path}\"") { UseShellExecute = true });
            else if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts.Cancel(); _cts.Dispose(); _pulse.Stop();
        base.OnClosed(e);
    }
}
