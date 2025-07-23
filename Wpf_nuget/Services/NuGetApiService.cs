using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Packaging;
using Wpf_nuget.Models;
using Newtonsoft.Json;
using System.IO.Compression;
using NuGet.Configuration;
using System.IO;
using NuGet.Packaging.Core;

namespace Wpf_nuget.Services;

/// <summary>
/// NuGet API服务
/// </summary>
public class NuGetApiService
{
    private readonly ILogger _logger;
    private readonly SourceRepository _repository;
    private readonly string _packagesDirectory;

    /// <summary>
    /// 构造函数
    /// </summary>
    public NuGetApiService()
    {
        _logger = NullLogger.Instance;
        var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
        _repository = Repository.Factory.GetCoreV3(packageSource);
        _packagesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packages");
        
        if (!Directory.Exists(_packagesDirectory))
        {
            Directory.CreateDirectory(_packagesDirectory);
        }
    }

    #region 搜索功能

    /// <summary>
    /// 搜索NuGet包
    /// </summary>
    /// <param name="searchTerm">搜索关键词</param>
    /// <param name="skip">跳过数量</param>
    /// <param name="take">获取数量</param>
    /// <param name="includePrerelease">是否包含预发布版本</param>
    /// <returns>搜索结果</returns>
    public async Task<IEnumerable<NuGetPackage>> SearchPackagesAsync(string searchTerm, int skip = 0, int take = 20, bool includePrerelease = false)
    {
        try
        {
            var searchResource = await _repository.GetResourceAsync<PackageSearchResource>();
            var searchFilter = new SearchFilter(includePrerelease);
            
            var results = await searchResource.SearchAsync(
                searchTerm,
                searchFilter,
                skip,
                take,
                _logger,
                CancellationToken.None);

            var packages = new List<NuGetPackage>();
            
            foreach (var result in results)
            {
                var package = new NuGetPackage
                {
                    Id = result.Identity.Id,
                    Title = result.Title ?? result.Identity.Id,
                    Description = result.Description ?? string.Empty,
                    Authors = result.Authors != null ? string.Join(", ", result.Authors) : string.Empty,
                    Tags = result.Tags != null ? string.Join(", ", result.Tags) : string.Empty,
                    ProjectUrl = result.ProjectUrl?.ToString() ?? string.Empty,
                    IconUrl = result.IconUrl?.ToString() ?? string.Empty,
                    LicenseUrl = result.LicenseUrl?.ToString() ?? string.Empty,
                    DownloadCount = (long)(result.DownloadCount ?? 0),
                    // IsVerified 属性在当前版本中不可用
                };

                // 获取版本信息
                var versions = await GetPackageVersionsAsync(result.Identity.Id);
                package.Versions = versions.ToList();

                packages.Add(package);
            }

            return packages;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"搜索包失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取包的所有版本
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <returns>版本列表</returns>
    public async Task<IEnumerable<NuGetPackageVersion>> GetPackageVersionsAsync(string packageId)
    {
        try
        {
            var metadataResource = await _repository.GetResourceAsync<PackageMetadataResource>();
            var packages = await metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: true,
                includeUnlisted: false,
                sourceCacheContext: new SourceCacheContext(),
                log: _logger,
                token: CancellationToken.None);

            var versions = new List<NuGetPackageVersion>();
            
            foreach (var package in packages.OrderByDescending(p => p.Identity.Version))
            {
                var version = new NuGetPackageVersion
                {
                    PackageId = packageId,
                    Version = package.Identity.Version.ToString(),
                    Published = package.Published?.DateTime ?? DateTime.Now,
                    DownloadUrl = GetDownloadUrl(packageId, package.Identity.Version.ToString()),
                    Dependencies = SerializeDependencies(package.DependencySets),
                    ReleaseNotes = string.Empty, // ReleaseNotes在IPackageSearchMetadata中不可用
                    IsPrerelease = package.Identity.Version.IsPrerelease
                };

                versions.Add(version);
            }

            return versions;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取包版本失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region 包信息获取

    /// <summary>
    /// 根据包ID获取包信息
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <returns>包信息</returns>
    public async Task<NuGetPackage?> GetPackageAsync(string packageId)
    {
        try
        {
            var metadataResource = await _repository.GetResourceAsync<PackageMetadataResource>();
            var packages = await metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: true,
                includeUnlisted: false,
                sourceCacheContext: new SourceCacheContext(),
                log: _logger,
                token: CancellationToken.None);

            var latestPackage = packages.OrderByDescending(p => p.Identity.Version).FirstOrDefault();
            if (latestPackage == null)
                return null;

            var package = new NuGetPackage
            {
                Id = latestPackage.Identity.Id,
                Title = latestPackage.Title ?? latestPackage.Identity.Id,
                Description = latestPackage.Description ?? string.Empty,
                Authors = latestPackage.Authors != null ? string.Join(", ", latestPackage.Authors) : string.Empty,
                Tags = latestPackage.Tags != null ? string.Join(", ", latestPackage.Tags) : string.Empty,
                ProjectUrl = latestPackage.ProjectUrl?.ToString() ?? string.Empty,
                IconUrl = latestPackage.IconUrl?.ToString() ?? string.Empty,
                LicenseUrl = latestPackage.LicenseUrl?.ToString() ?? string.Empty,
                DownloadCount = (long)(latestPackage.DownloadCount ?? 0)
            };

            // 获取版本信息
            var versions = await GetPackageVersionsAsync(packageId);
            package.Versions = versions.ToList();

            return package;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取包信息失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 根据包ID和版本号获取特定版本信息
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <returns>版本信息</returns>
    public async Task<NuGetPackageVersion?> GetPackageVersionAsync(string packageId, string version)
    {
        try
        {
            var versions = await GetPackageVersionsAsync(packageId);
            return versions.FirstOrDefault(v => v.Version == version);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取包版本信息失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region 下载功能

    /// <summary>
    /// 下载NuGet包
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <returns>相对于程序目录的文件路径</returns>
    public async Task<string> DownloadPackageAsync(string packageId, string version)
    {
        try
        {
            var packageVersion = NuGetVersion.Parse(version);
            var packageIdentity = new PackageIdentity(packageId, packageVersion);
            
            var downloadResource = await _repository.GetResourceAsync<DownloadResource>();
            
            var packagePath = Path.Combine(_packagesDirectory, $"{packageId}.{version}.nupkg");
            
            using var packageStream = File.Create(packagePath);
            var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                packageIdentity,
                new PackageDownloadContext(new SourceCacheContext()),
                Path.GetDirectoryName(packagePath),
                _logger,
                CancellationToken.None);

            if (downloadResult.Status == DownloadResourceResultStatus.Available)
            {
                await downloadResult.PackageStream.CopyToAsync(packageStream);
                // 返回相对路径而不是完整路径
                return Path.Combine("packages", $"{packageId}.{version}.nupkg");
            }
            else
            {
                throw new InvalidOperationException($"包下载失败: {downloadResult.Status}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"下载包失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 提取包内容
    /// </summary>
    /// <param name="packagePath">包文件路径</param>
    /// <param name="extractPath">提取目录</param>
    public async Task ExtractPackageAsync(string packagePath, string extractPath)
    {
        try
        {
            if (!Directory.Exists(extractPath))
            {
                Directory.CreateDirectory(extractPath);
            }

            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(packagePath);
                archive.ExtractToDirectory(extractPath, overwriteFiles: true);
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"提取包失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取下载URL
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <returns>下载URL</returns>
    private string GetDownloadUrl(string packageId, string version)
    {
        return $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{version.ToLowerInvariant()}/{packageId.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg";
    }

    /// <summary>
    /// 序列化依赖项
    /// </summary>
    /// <param name="dependencySets">依赖项集合</param>
    /// <returns>JSON字符串</returns>
    private string SerializeDependencies(IEnumerable<PackageDependencyGroup> dependencySets)
    {
        try
        {
            var dependencies = dependencySets.Select(group => new
            {
                TargetFramework = group.TargetFramework?.GetShortFolderName() ?? "any",
                Dependencies = group.Packages.Select(dep => new
                {
                    Id = dep.Id,
                    VersionRange = dep.VersionRange?.ToString() ?? "*"
                })
            });

            return JsonConvert.SerializeObject(dependencies, Formatting.None);
        }
        catch
        {
            return "[]";
        }
    }

    /// <summary>
    /// 根据相对路径获取完整路径
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns>完整路径</returns>
    public string GetFullPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return string.Empty;
            
        // 如果已经是完整路径，直接返回
        if (Path.IsPathRooted(relativePath))
            return relativePath;
            
        // 组合程序目录和相对路径
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
    }

    /// <summary>
    /// 获取包文件大小
    /// </summary>
    /// <param name="packagePath">包文件路径（可以是相对路径或完整路径）</param>
    /// <returns>文件大小（字节）</returns>
    public long GetPackageFileSize(string packagePath)
    {
        try
        {
            var fullPath = GetFullPath(packagePath);
            if (File.Exists(fullPath))
            {
                return new FileInfo(fullPath).Length;
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    #endregion
}