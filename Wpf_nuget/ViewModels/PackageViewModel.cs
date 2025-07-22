using System;
using System.ComponentModel;
using Wpf_nuget.Models;

namespace Wpf_nuget.ViewModels;

/// <summary>
/// NuGet包的视图模型
/// </summary>
public class PackageViewModel : INotifyPropertyChanged
{
    #region 私有字段

    private readonly NuGetPackage _package;

    #endregion

    #region 属性

    /// <summary>
    /// 包ID
    /// </summary>
    public string Id => _package.Id;

    /// <summary>
    /// 包标题
    /// </summary>
    public string Title => _package.Title ?? _package.Id;

    /// <summary>
    /// 包描述
    /// </summary>
    public string Description => _package.Description ?? "";

    /// <summary>
    /// 作者
    /// </summary>
    public string Authors => _package.Authors ?? "";

    /// <summary>
    /// 下载次数
    /// </summary>
    public long DownloadCount => _package.DownloadCount;

    /// <summary>
    /// 项目URL
    /// </summary>
    public string? ProjectUrl => _package.ProjectUrl;

    /// <summary>
    /// 标签
    /// </summary>
    public string Tags => _package.Tags ?? "";

    /// <summary>
    /// 图标URL
    /// </summary>
    public string? IconUrl => _package.IconUrl;

    /// <summary>
    /// 许可证URL
    /// </summary>
    public string? LicenseUrl => _package.LicenseUrl;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime => _package.CreatedTime;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedTime => _package.UpdatedTime;

    /// <summary>
    /// 是否有本地版本
    /// </summary>
    public bool HasLocalVersions => _package.Versions?.Any(v => v.IsDownloaded) ?? false;

    /// <summary>
    /// 本地版本数量
    /// </summary>
    public int LocalVersionCount => _package.Versions?.Count(v => v.IsDownloaded) ?? 0;

    /// <summary>
    /// 总版本数量
    /// </summary>
    public int TotalVersionCount => _package.Versions?.Count ?? 0;

    /// <summary>
    /// 最新版本
    /// </summary>
    public string? LatestVersion => _package.Versions?
        .OrderByDescending(v => v.Published)
        .FirstOrDefault()?.Version;

    /// <summary>
    /// 格式化的下载次数
    /// </summary>
    public string FormattedDownloadCount
    {
        get
        {
            if (DownloadCount >= 1_000_000)
                return $"{DownloadCount / 1_000_000.0:F1}M";
            if (DownloadCount >= 1_000)
                return $"{DownloadCount / 1_000.0:F1}K";
            return DownloadCount.ToString();
        }
    }

    /// <summary>
    /// 状态描述
    /// </summary>
    public string StatusDescription
    {
        get
        {
            if (HasLocalVersions)
                return $"已下载 {LocalVersionCount}/{TotalVersionCount} 个版本";
            return "未下载";
        }
    }

    /// <summary>
    /// 状态
    /// </summary>
    public string Status
    {
        get
        {
            if (HasLocalVersions)
                return "已下载";
            return "未下载";
        }
    }

    /// <summary>
    /// 状态颜色
    /// </summary>
    public string StatusColor
    {
        get
        {
            if (HasLocalVersions)
                return "Green";
            return "Gray";
        }
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化PackageViewModel
    /// </summary>
    /// <param name="package">NuGet包模型</param>
    public PackageViewModel(NuGetPackage package)
    {
        _package = package ?? throw new ArgumentNullException(nameof(package));
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region 转换方法

    /// <summary>
    /// 转换为NuGetPackage模型
    /// </summary>
    /// <returns>NuGetPackage对象</returns>
    public NuGetPackage ToNuGetPackage()
    {
        return _package;
    }

    #endregion

    #region 重写方法

    public override string ToString()
    {
        return $"{Id} - {Title}";
    }

    public override bool Equals(object? obj)
    {
        return obj is PackageViewModel other && Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}