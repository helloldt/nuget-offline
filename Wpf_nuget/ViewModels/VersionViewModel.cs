using System;
using System.ComponentModel;
using System.IO;
using Wpf_nuget.Models;

namespace Wpf_nuget.ViewModels;

/// <summary>
/// NuGet包版本的视图模型
/// </summary>
public class VersionViewModel : INotifyPropertyChanged
{
    #region 私有字段

    private readonly NuGetPackageVersion _version;

    #endregion

    #region 属性

    /// <summary>
    /// 版本ID
    /// </summary>
    public int Id => _version.Id;

    /// <summary>
    /// 包ID
    /// </summary>
    public string PackageId => _version.PackageId;

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version => _version.Version;

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime Published => _version.Published;

    /// <summary>
    /// 下载URL
    /// </summary>
    public string? DownloadUrl => _version.DownloadUrl;

    /// <summary>
    /// 文件大小
    /// </summary>
    public long FileSize => _version.FileSize;

    /// <summary>
    /// 依赖项
    /// </summary>
    public string? Dependencies => _version.Dependencies;

    /// <summary>
    /// 发布说明
    /// </summary>
    public string? ReleaseNotes => _version.ReleaseNotes;

    /// <summary>
    /// 是否为预发布版本
    /// </summary>
    public bool IsPrerelease => _version.IsPrerelease;

    /// <summary>
    /// 本地路径
    /// </summary>
    public string? LocalPath => _version.LocalPath;

    /// <summary>
    /// 是否已下载
    /// </summary>
    public bool IsDownloaded => _version.IsDownloaded;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime => _version.CreatedTime;

    /// <summary>
    /// 格式化的发布时间
    /// </summary>
    public string FormattedPublished
    {
        get
        {
            var timeSpan = DateTime.Now - Published;
            if (timeSpan.TotalDays < 1)
                return "今天";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} 天前";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} 周前";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} 月前";
            return $"{(int)(timeSpan.TotalDays / 365)} 年前";
        }
    }

    /// <summary>
    /// 格式化的文件大小
    /// </summary>
    public string FormattedFileSize
    {
        get
        {
            if (FileSize == 0)
                return "未知";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = FileSize;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// 版本类型描述
    /// </summary>
    public string VersionTypeDescription
    {
        get
        {
            if (IsPrerelease)
                return "预发布";
            return "正式版";
        }
    }

    /// <summary>
    /// 下载状态描述
    /// </summary>
    public string DownloadStatusDescription
    {
        get
        {
            if (IsDownloaded)
            {
                if (LocalFileExists())
                    return "已下载";
                return "文件丢失";
            }
            return "未下载";
        }
    }

    /// <summary>
    /// 状态图标
    /// </summary>
    public string StatusIcon
    {
        get
        {
            if (IsDownloaded)
            {
                if (LocalFileExists())
                    return "✓";
                return "⚠";
            }
            return "○";
        }
    }

    /// <summary>
    /// 依赖项数量
    /// </summary>
    public int DependencyCount
    {
        get
        {
            if (string.IsNullOrEmpty(Dependencies))
                return 0;
            
            try
            {
                var deps = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(Dependencies);
                if (deps is Newtonsoft.Json.Linq.JArray array)
                    return array.Count;
                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// 依赖项描述
    /// </summary>
    public string DependencyDescription
    {
        get
        {
            var count = DependencyCount;
            if (count == 0)
                return "无依赖";
            return $"{count} 个依赖";
        }
    }

    /// <summary>
    /// 完整描述
    /// </summary>
    public string FullDescription
    {
        get
        {
            var parts = new List<string> { Version };
            
            if (IsPrerelease)
                parts.Add("预发布");
            
            parts.Add(FormattedPublished);
            parts.Add(FormattedFileSize);
            
            if (DependencyCount > 0)
                parts.Add(DependencyDescription);
            
            return string.Join(" • ", parts);
        }
    }

    /// <summary>
    /// 状态
    /// </summary>
    public string Status
    {
        get
        {
            if (IsDownloaded)
            {
                if (LocalFileExists())
                    return "已下载";
                return "文件丢失";
            }
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
            if (IsDownloaded)
            {
                if (LocalFileExists())
                    return "Green";
                return "Red";
            }
            return "Gray";
        }
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化VersionViewModel
    /// </summary>
    /// <param name="version">NuGet包版本模型</param>
    public VersionViewModel(NuGetPackageVersion version)
    {
        _version = version ?? throw new ArgumentNullException(nameof(version));
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 获取完整路径
    /// </summary>
    /// <returns>完整路径</returns>
    private string GetFullPath()
    {
        if (string.IsNullOrEmpty(LocalPath))
            return string.Empty;
            
        // 如果已经是完整路径，直接返回
        if (Path.IsPathRooted(LocalPath))
            return LocalPath;
            
        // 组合程序目录和相对路径
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LocalPath);
    }

    /// <summary>
    /// 获取本地文件信息
    /// </summary>
    /// <returns>文件信息，如果文件不存在则返回null</returns>
    public FileInfo? GetLocalFileInfo()
    {
        var fullPath = GetFullPath();
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            return null;
        
        return new FileInfo(fullPath);
    }

    /// <summary>
    /// 检查本地文件是否存在
    /// </summary>
    /// <returns>文件是否存在</returns>
    public bool LocalFileExists()
    {
        var fullPath = GetFullPath();
        return !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath);
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
    /// 转换为NuGetPackageVersion模型
    /// </summary>
    /// <returns>NuGetPackageVersion对象</returns>
    public NuGetPackageVersion ToNuGetPackageVersion()
    {
        return _version;
    }

    #endregion

    #region 重写方法

    public override string ToString()
    {
        return $"{PackageId} v{Version}";
    }

    public override bool Equals(object? obj)
    {
        return obj is VersionViewModel other && 
               PackageId.Equals(other.PackageId, StringComparison.OrdinalIgnoreCase) &&
               Version.Equals(other.Version, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            PackageId.GetHashCode(StringComparison.OrdinalIgnoreCase),
            Version.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}