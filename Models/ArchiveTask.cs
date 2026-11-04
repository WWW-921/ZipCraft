using SimpleCompress.Models;

namespace SimpleCompress.Models;

/// <summary>压缩请求参数。</summary>
public class CompressRequest
{
    public List<string> Inputs { get; set; } = new();
    public string OutputArchive { get; set; } = "";   // 完整输出文件路径
    public ArchiveFormat Format { get; set; } = ArchiveFormat.Zip;
    public CompressionLevel Level { get; set; } = CompressionLevel.Normal;
    public string? Password { get; set; }
    public bool EncryptHeaders { get; set; }            // 仅 7z 支持
    public string? VolumeSize { get; set; }             // 如 "100m"、"1g"；null 不分卷
}

/// <summary>解压请求参数。</summary>
public class ExtractRequest
{
    public string Archive { get; set; } = "";
    public string Destination { get; set; } = "";       // 目标目录
    public bool FlattenPaths { get; set; } = false;      // false=x 保留结构
    public string? Password { get; set; }
    public ConflictPolicy Conflict { get; set; } = ConflictPolicy.Overwrite;
}

public enum TaskKind { Compress, Extract, Test, List }

/// <summary>任务运行结果。</summary>
public class TaskResult
{
    public bool Success { get; set; }
    public bool Cancelled { get; set; }
    public int ExitCode { get; set; }
    public string? ErrorKind { get; set; }              // 分类键：WrongPassword/NoSpace/AccessDenied/Corrupt/Unsupported/None
    public string? ErrorMessage { get; set; }           // 中文说明
    public string? OutputPath { get; set; }
    public List<string> Log { get; set; } = new();      // 已脱敏
    public string Output { get; set; } = "";            // 引擎合并输出（已脱敏）
}

/// <summary>压缩包内一条文件记录。</summary>
public record EntryInfo(string Path, long Size, bool IsDir);
