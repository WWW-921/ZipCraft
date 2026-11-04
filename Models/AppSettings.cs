using System.Text.Json.Serialization;

namespace SimpleCompress.Models;

/// <summary>应用配置，持久化为 JSON，影响默认行为。</summary>
public class AppSettings
{
    /// <summary>是否在文件右键菜单显示压缩/解压入口（注册状态镜像）。</summary>
    public bool ShellMenuEnabled { get; set; } = false;

    /// <summary>默认压缩格式：zip / 7z。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ArchiveFormat DefaultFormat { get; set; } = ArchiveFormat.Zip;

    /// <summary>默认解压到同名文件夹。</summary>
    public bool ExtractToNamedFolder { get; set; } = true;

    /// <summary>解压完成后默认打开文件夹。</summary>
    public bool OpenFolderAfterExtract { get; set; } = true;

    /// <summary>默认压缩等级：fast / normal / max。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompressionLevel DefaultLevel { get; set; } = CompressionLevel.Normal;

    /// <summary>同名文件默认策略：ask / skip / overwrite / rename。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConflictPolicy DefaultConflict { get; set; } = ConflictPolicy.Ask;

    /// <summary>引擎（7z.exe）路径，留空则自动发现。</summary>
    public string EnginePath { get; set; } = "";

    /// <summary>PeaZip 完整界面路径，留空则自动发现。</summary>
    public string PeaZipPath { get; set; } = "";
}

public enum ArchiveFormat { Zip, SevenZip }

public enum CompressionLevel { Fast, Normal, Maximum }

public enum ConflictPolicy { Ask, Skip, Overwrite, Rename }
