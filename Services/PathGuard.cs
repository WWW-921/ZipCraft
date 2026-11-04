using System.IO;

namespace SimpleCompress.Services;

/// <summary>
/// 解压路径安全：识别压缩包内的绝对路径、盘符路径与越界（../）条目，
/// 保证写入范围不超出用户选择的目标目录。7z 默认会把条目当作相对路径处理，
/// 本类提供显式判定，供上层提示与规范化。
/// </summary>
public static class PathGuard
{
    /// <summary>条目是否为越界/绝对路径等不安全项。</summary>
    public static bool IsUnsafe(string entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath)) return false;
        var p = entryPath.Replace('\\', '/');
        if (p.Contains(":")) return true;                       // 含盘符
        if (p.StartsWith("/")) return true;                     // 绝对路径
        var segs = p.Split('/');
        foreach (var s in segs)
            if (s == "..") return true;                          // 目录穿越
        return false;
    }

    /// <summary>把不安全条目规范到目标目录内（去除盘符与 ../）。</summary>
    public static string SanitizeInto(string destination, string entryPath)
    {
        var p = entryPath.Replace('\\', '/');
        int colon = p.IndexOf(':');
        if (colon >= 0 && colon <= 2) p = p.Substring(colon + 1); // 去盘符
        var segs = p.Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => s != "..").ToArray();
        var combined = Path.Combine(destination,
            string.Join(Path.DirectorySeparatorChar.ToString(), segs));
        var fullDest = Path.GetFullPath(destination)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullCombined = Path.GetFullPath(combined);
        if (!fullCombined.StartsWith(fullDest + Path.DirectorySeparatorChar)
            && fullCombined != fullDest)
            return Path.Combine(fullDest, Path.GetFileName(segs.Length > 0 ? segs[^1] : "file"));
        return fullCombined;
    }

    /// <summary>目标可写性检查。</summary>
    public static bool IsWritableDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;
            var probe = Path.Combine(dir, ".write_test_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}
