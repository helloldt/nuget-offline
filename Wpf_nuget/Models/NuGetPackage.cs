using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wpf_nuget.Models;

/// <summary>
/// NuGet包实体类
/// </summary>
public class NuGetPackage
{
    /// <summary>
    /// 包ID
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 包标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 包描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 作者
    /// </summary>
    public string Authors { get; set; } = string.Empty;

    /// <summary>
    /// 标签
    /// </summary>
    public string Tags { get; set; } = string.Empty;

    /// <summary>
    /// 项目URL
    /// </summary>
    public string ProjectUrl { get; set; } = string.Empty;

    /// <summary>
    /// 图标URL
    /// </summary>
    public string IconUrl { get; set; } = string.Empty;

    /// <summary>
    /// 许可证URL
    /// </summary>
    public string LicenseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 摘要
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 版权信息
    /// </summary>
    public string Copyright { get; set; } = string.Empty;

    /// <summary>
    /// 是否需要许可证接受
    /// </summary>
    public bool RequireLicenseAcceptance { get; set; }

    /// <summary>
    /// 语言
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 发布说明
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// 最小客户端版本
    /// </summary>
    public string MinClientVersion { get; set; } = string.Empty;

    /// <summary>
    /// 是否为预发布版本
    /// </summary>
    public bool IsPrerelease { get; set; }

    /// <summary>
    /// 是否已列出
    /// </summary>
    public bool Listed { get; set; } = true;

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTimeOffset Published { get; set; }

    /// <summary>
    /// 包数据
    /// </summary>
    public byte[]? PackageData { get; set; }

    /// <summary>
    /// 下载次数
    /// </summary>
    public long DownloadCount { get; set; }

    /// <summary>
    /// 是否已验证
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 包版本列表
    /// </summary>
    public virtual ICollection<NuGetPackageVersion> Versions { get; set; } = new List<NuGetPackageVersion>();
}

/// <summary>
/// NuGet包版本实体类
/// </summary>
public class NuGetPackageVersion
{
    /// <summary>
    /// 版本ID
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 包ID
    /// </summary>
    [ForeignKey(nameof(Package))]
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime Published { get; set; }

    /// <summary>
    /// 下载URL
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 依赖项（JSON格式）
    /// </summary>
    public string Dependencies { get; set; } = string.Empty;

    /// <summary>
    /// 发布说明
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// 是否为预发布版本
    /// </summary>
    public bool IsPrerelease { get; set; }

    /// <summary>
    /// 本地文件路径
    /// </summary>
    public string? LocalPath { get; set; }

    /// <summary>
    /// 是否已下载
    /// </summary>
    public bool IsDownloaded { get; set; }

    /// <summary>
    /// 是否已列出（用于软删除）
    /// </summary>
    public bool Listed { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 关联的包
    /// </summary>
    public virtual NuGetPackage Package { get; set; } = null!;
}