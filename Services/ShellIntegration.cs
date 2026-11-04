using System.IO;
using Microsoft.Win32;
using SimpleCompress.Services;

namespace SimpleCompress.Services;

/// <summary>
/// 右键菜单集成（免管理员，写入 HKCU\Software\Classes）。
/// - 普通文件/文件夹：压缩为 ZIP、压缩到……。
/// - 压缩包（按扩展名关联）：解压到同名文件夹、解压到当前文件夹、解压到……、打开压缩包。
/// 注册记录当前 exe 绝对路径；程序移动后需在设置里重新启用。可完整移除。
/// Windows 11 下这些命令出现在“显示更多选项”经典菜单中。
/// </summary>
public static class ShellIntegration
{
    private const string AppKey = "SimpleCompress";

    // 命令行子动词
    public const string CmdCompressQuick = "compress-quick";
    public const string CmdCompressDialog = "compress-dialog";
    public const string CmdExtractQuick = "extract-quick";
    public const string CmdExtractHere = "extract-here";
    public const string CmdExtractDialog = "extract-dialog";
    public const string CmdBrowse = "browse";

    private static readonly string[] ArchiveExts =
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".iso", ".zst", ".cab" };

    public static string ExePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ZipCraft.exe");

    private static string Cmd(string verb) =>
        $"\"{ExePath}\" --cmd {verb} \"%1\" %U";

    /// <summary>当前是否已注册（检查代表项）。</summary>
    public static bool IsRegistered()
    {
        using var k = Registry.CurrentUser.OpenSubKey(
            $@"Software\Classes\*\shell\{AppKey}.compresszip");
        return k != null;
    }

    public static void Register()
    {
        // 普通文件（含文件夹通过 directory）：压缩入口
        AddVerb($@"Software\Classes\*\shell\{AppKey}.compresszip",
            "压缩为 ZIP", Cmd(CmdCompressQuick), iconIndex: null);
        AddVerb($@"Software\Classes\*\shell\{AppKey}.compressto",
            "压缩到……", Cmd(CmdCompressDialog), null);

        AddVerb($@"Software\Classes\directory\shell\{AppKey}.compresszip",
            "压缩为 ZIP", Cmd(CmdCompressQuick), null);
        AddVerb($@"Software\Classes\directory\shell\{AppKey}.compressto",
            "压缩到……", Cmd(CmdCompressDialog), null);

        // 压缩包：解压与浏览入口（按扩展名关联，避免污染普通文件菜单）
        foreach (var ext in ArchiveExts)
        {
            string baseKey = $@"Software\Classes\SystemFileAssociations\{ext}\shell";
            AddVerb($@"{baseKey}\{AppKey}.extractfolder",
                "解压到同名文件夹", Cmd(CmdExtractQuick), null);
            AddVerb($@"{baseKey}\{AppKey}.extracthere",
                "解压到当前文件夹", Cmd(CmdExtractHere), null);
            AddVerb($@"{baseKey}\{AppKey}.extractto",
                "解压到……", Cmd(CmdExtractDialog), null);
            AddVerb($@"{baseKey}\{AppKey}.browse",
                "打开压缩包", Cmd(CmdBrowse), null);
        }
    }

    public static void Unregister()
    {
        Delete($@"Software\Classes\*\shell\{AppKey}.compresszip");
        Delete($@"Software\Classes\*\shell\{AppKey}.compressto");
        Delete($@"Software\Classes\directory\shell\{AppKey}.compresszip");
        Delete($@"Software\Classes\directory\shell\{AppKey}.compressto");
        foreach (var ext in ArchiveExts)
        {
            string baseKey = $@"Software\Classes\SystemFileAssociations\{ext}\shell";
            Delete($@"{baseKey}\{AppKey}.extractfolder");
            Delete($@"{baseKey}\{AppKey}.extracthere");
            Delete($@"{baseKey}\{AppKey}.extractto");
            Delete($@"{baseKey}\{AppKey}.browse");
        }
    }

    private static void AddVerb(string shellKey, string label, string command, string? iconIndex)
    {
        using var shell = Registry.CurrentUser.CreateSubKey(shellKey);
        if (shell == null) return;
        shell.SetValue("FriendlyAppName", "小压匠");
        shell.SetValue("MUIVerb", label);   // 部分壳读取；同时用默认值兜底
        shell.SetValue("", label);
        if (iconIndex != null) shell.SetValue("Icon", iconIndex);
        using var cmd = shell.CreateSubKey("command");
        cmd?.SetValue("", command);
    }

    private static void Delete(string fullKey)
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(fullKey, throwOnMissingSubKey: false); }
        catch { /* 忽略不存在 */ }
    }
}
