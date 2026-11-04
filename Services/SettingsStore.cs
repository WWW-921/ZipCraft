using System.IO;
using System.Text.Json;
using SimpleCompress.Models;

namespace SimpleCompress.Services;

/// <summary>配置读写。优先程序目录 settings.json（绿色便携）。</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string BaseDir =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    private static string FilePath => Path.Combine(BaseDir, "settings.json");

    private static AppSettings? _cache;

    public static AppSettings Load()
    {
        if (_cache != null) return _cache;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _cache = JsonSerializer.Deserialize<AppSettings>(json, Opt) ?? new AppSettings();
            }
            else
            {
                _cache = new AppSettings();
            }
        }
        catch
        {
            _cache = new AppSettings();
        }
        return _cache;
    }

    public static void Save(AppSettings s)
    {
        _cache = s;
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(s, Opt));
        }
        catch
        {
            // 程序目录不可写时退回用户目录
            try
            {
                var alt = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimpleCompress");
                Directory.CreateDirectory(alt);
                File.WriteAllText(Path.Combine(alt, "settings.json"),
                    JsonSerializer.Serialize(s, Opt));
            }
            catch { /* 忽略 */ }
        }
    }
}
