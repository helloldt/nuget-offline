using Microsoft.EntityFrameworkCore;
using System.IO;
using Wpf_nuget.Data;
using Wpf_nuget.Models;

namespace Wpf_nuget.Services;

/// <summary>
/// 包数据库服务
/// </summary>
public class PackageDbService
{
    private readonly NuGetDbContext _context;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="context">数据库上下文</param>
    public PackageDbService(NuGetDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public PackageDbService()
    {
        _context = new NuGetDbContext();
    }

    #region 包管理

    /// <summary>
    /// 搜索本地包
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
            var query = _context.Packages
                .Include(p => p.Versions)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(p => 
                    p.Id.ToLower().Contains(term) ||
                    p.Title.ToLower().Contains(term) ||
                    p.Description.ToLower().Contains(term) ||
                    p.Tags.ToLower().Contains(term) ||
                    p.Authors.ToLower().Contains(term));
            }

            if (!includePrerelease)
            {
                query = query.Where(p => p.Versions.Any(v => !v.IsPrerelease));
            }

            var packages = await query
                .OrderByDescending(p => p.DownloadCount)
                .ThenBy(p => p.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return packages;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"搜索本地包失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 根据ID获取包
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <returns>包信息</returns>
    public async Task<NuGetPackage?> GetPackageByIdAsync(string packageId)
    {
        try
        {
            return await _context.Packages
                .Include(p => p.Versions)
                .FirstOrDefaultAsync(p => p.Id == packageId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取包失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 添加或更新包
    /// </summary>
    /// <param name="package">包信息</param>
    /// <returns>是否成功</returns>
    public async Task<bool> AddOrUpdatePackageAsync(NuGetPackage package)
    {
        try
        {
            // 检查传入的package是否被跟踪，如果是则分离
            var trackedEntity = _context.Entry(package);
            if (trackedEntity.State != EntityState.Detached)
            {
                trackedEntity.State = EntityState.Detached;
            }
            
            var existingPackage = await _context.Packages
                .Include(p => p.Versions)
                .FirstOrDefaultAsync(p => p.Id == package.Id);

            if (existingPackage == null)
            {
                // 添加新包时，先创建包实体，不包含版本信息
                var newPackage = new NuGetPackage
                {
                    Id = package.Id,
                    Title = package.Title,
                    Description = package.Description,
                    Authors = package.Authors,
                    Tags = package.Tags,
                    ProjectUrl = package.ProjectUrl,
                    IconUrl = package.IconUrl,
                    LicenseUrl = package.LicenseUrl,
                    Version = package.Version,
                    Summary = package.Summary,
                    Copyright = package.Copyright,
                    RequireLicenseAcceptance = package.RequireLicenseAcceptance,
                    Language = package.Language,
                    ReleaseNotes = package.ReleaseNotes,
                    MinClientVersion = package.MinClientVersion,
                    IsPrerelease = package.IsPrerelease,
                    Listed = package.Listed,
                    Published = package.Published,
                    DownloadCount = package.DownloadCount,
                    IsVerified = package.IsVerified,
                    CreatedTime = DateTime.Now,
                    UpdatedTime = DateTime.Now,
                    Versions = new List<NuGetPackageVersion>()
                };
                _context.Packages.Add(newPackage);
                
                // 先保存包信息
                await _context.SaveChangesAsync();
                
                // 然后添加版本信息
                if (package.Versions?.Any() == true)
                {
                    await UpdatePackageVersionsAsync(newPackage, package.Versions);
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                // 更新现有包
                existingPackage.Title = package.Title;
                existingPackage.Description = package.Description;
                existingPackage.Authors = package.Authors;
                existingPackage.Tags = package.Tags;
                existingPackage.ProjectUrl = package.ProjectUrl;
                existingPackage.IconUrl = package.IconUrl;
                existingPackage.LicenseUrl = package.LicenseUrl;
                existingPackage.DownloadCount = package.DownloadCount;
                existingPackage.IsVerified = package.IsVerified;
                existingPackage.UpdatedTime = DateTime.Now;

                // 更新版本信息
                if (package.Versions?.Any() == true)
                {
                    await UpdatePackageVersionsAsync(existingPackage, package.Versions);
                }
                
                await _context.SaveChangesAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            // 获取详细的错误信息
            var errorMessage = $"添加或更新包失败: {ex.Message}";
            
            // 如果是Entity Framework相关异常，获取更详细的信息
            if (ex.InnerException != null)
            {
                errorMessage += $"\n内部异常: {ex.InnerException.Message}";
                
                // 如果有更深层的异常
                if (ex.InnerException.InnerException != null)
                {
                    errorMessage += $"\n深层异常: {ex.InnerException.InnerException.Message}";
                }
            }
            
            // 记录完整的异常堆栈
            errorMessage += $"\n堆栈跟踪: {ex.StackTrace}";
            
            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    /// <summary>
    /// 删除包
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <returns>是否成功</returns>
    public async Task<bool> DeletePackageAsync(string packageId)
    {
        try
        {
            var package = await _context.Packages
                .Include(p => p.Versions)
                .FirstOrDefaultAsync(p => p.Id == packageId);

            if (package != null)
            {
                _context.Packages.Remove(package);
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"删除包失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region 单包操作

    /// <summary>
    /// 获取指定包和版本
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <returns>包信息</returns>
    public async Task<NuGetPackage?> GetPackageAsync(string packageId, string version)
    {
        try
        {
            return await _context.Packages
                .Include(p => p.Versions)
                .FirstOrDefaultAsync(p => p.Id == packageId && p.Version == version);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取包失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 添加包
    /// </summary>
    /// <param name="package">包信息</param>
    /// <returns>添加的包</returns>
    public async Task<NuGetPackage> AddPackageAsync(NuGetPackage package)
    {
        try
        {
            package.CreatedTime = DateTime.Now;
            package.UpdatedTime = DateTime.Now;
            _context.Packages.Add(package);
            await _context.SaveChangesAsync();
            return package;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"添加包失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 更新包
    /// </summary>
    /// <param name="package">包信息</param>
    /// <returns>是否成功</returns>
    public async Task<bool> UpdatePackageAsync(NuGetPackage package)
    {
        try
        {
            package.UpdatedTime = DateTime.Now;
            _context.Packages.Update(package);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"更新包失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region 版本管理

    /// <summary>
    /// 获取包的所有版本
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <returns>版本列表</returns>
    public async Task<IEnumerable<NuGetPackageVersion>> GetPackageVersionsAsync(string packageId)
    {
        try
        {
            return await _context.PackageVersions
                .Where(v => v.PackageId == packageId)
                .OrderByDescending(v => v.Published)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取包版本失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取特定版本
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <returns>版本信息</returns>
    public async Task<NuGetPackageVersion?> GetPackageVersionAsync(string packageId, string version)
    {
        try
        {
            return await _context.PackageVersions
                .FirstOrDefaultAsync(v => v.PackageId == packageId && v.Version == version);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取包版本失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 更新版本的本地路径
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <param name="localPath">本地路径（相对路径）</param>
    /// <param name="fileSize">文件大小</param>
    /// <returns>是否成功</returns>
    public async Task<bool> UpdateVersionLocalPathAsync(string packageId, string version, string localPath, long fileSize)
    {
        try
        {
            var packageVersion = await _context.PackageVersions
                .FirstOrDefaultAsync(v => v.PackageId == packageId && v.Version == version);

            if (packageVersion != null)
            {
                packageVersion.LocalPath = localPath;
                packageVersion.FileSize = fileSize;
                
                // 检查文件存在性时使用完整路径
                var fullPath = GetFullPath(localPath);
                packageVersion.IsDownloaded = !string.IsNullOrEmpty(localPath) && File.Exists(fullPath);
                
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"更新版本本地路径失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 更新包版本下载状态
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <param name="isDownloaded">是否已下载</param>
    /// <param name="localPath">本地路径（相对路径）</param>
    /// <returns>是否成功</returns>
    public async Task<bool> UpdateVersionDownloadStatusAsync(string packageId, string version, bool isDownloaded, string localPath = null)
    {
        try
        {
            var packageVersion = await _context.PackageVersions
                .FirstOrDefaultAsync(v => v.PackageId == packageId && v.Version == version);

            if (packageVersion != null)
            {
                packageVersion.IsDownloaded = isDownloaded;
                
                if (!string.IsNullOrEmpty(localPath))
                {
                    packageVersion.LocalPath = localPath;
                    
                    // 如果提供了路径，检查文件存在性
                    var fullPath = GetFullPath(localPath);
                    packageVersion.IsDownloaded = isDownloaded && File.Exists(fullPath);
                }
                
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"更新版本下载状态失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 根据相对路径获取完整路径
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns>完整路径</returns>
    private string GetFullPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return string.Empty;
            
        // 如果已经是完整路径，直接返回
        if (Path.IsPathRooted(relativePath))
            return relativePath;
            
        // 组合程序目录和相对路径
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
    }

    #endregion

    #region 统计信息

    /// <summary>
    /// 获取包总数
    /// </summary>
    /// <returns>包总数</returns>
    public async Task<int> GetPackageCountAsync()
    {
        try
        {
            return await _context.Packages.CountAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取包总数失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取已下载的包数量
    /// </summary>
    /// <returns>已下载包数量</returns>
    public async Task<int> GetDownloadedPackageCountAsync()
    {
        try
        {
            return await _context.PackageVersions
                .Where(v => v.IsDownloaded)
                .Select(v => v.PackageId)
                .Distinct()
                .CountAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取已下载包数量失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取热门包
    /// </summary>
    /// <param name="count">数量</param>
    /// <returns>热门包列表</returns>
    public async Task<IEnumerable<NuGetPackage>> GetPopularPackagesAsync(int count = 10)
    {
        try
        {
            return await _context.Packages
                .Include(p => p.Versions)
                .OrderByDescending(p => p.DownloadCount)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取热门包失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region 包发布管理

    /// <summary>
    /// 检查包是否存在
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <returns>是否存在</returns>
    public async Task<bool> PackageExistsAsync(string packageId, string version)
    {
        try
        {
            return await _context.PackageVersions
                .AnyAsync(v => v.PackageId == packageId && v.Version == version);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"检查包是否存在失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 添加包版本
    /// </summary>
    /// <param name="packageVersion">包版本信息</param>
    /// <returns>是否成功</returns>
    public async Task<bool> AddPackageVersionAsync(NuGetPackageVersion packageVersion)
    {
        try
        {
            // 检查传入的版本对象是否已被跟踪，如果是则分离
            var trackedVersion = _context.Entry(packageVersion);
            if (trackedVersion.State != EntityState.Detached)
            {
                trackedVersion.State = EntityState.Detached;
            }

            // 检查该版本是否已存在
            var existingVersion = await _context.PackageVersions
                .FirstOrDefaultAsync(v => v.PackageId == packageVersion.PackageId && v.Version == packageVersion.Version);
            
            if (existingVersion != null)
            {
                // 版本已存在，不需要重复添加
                return true;
            }

            // 检查包是否存在，如果不存在则创建
            var package = await _context.Packages
                .FirstOrDefaultAsync(p => p.Id == packageVersion.PackageId);

            if (package == null)
            {
                package = new NuGetPackage
                {
                    Id = packageVersion.PackageId,
                    Title = packageVersion.PackageId,
                    Description = "",
                    Authors = "",
                    Tags = "",
                    CreatedTime = DateTime.Now,
                    UpdatedTime = DateTime.Now,
                    DownloadCount = 0,
                    IsVerified = false,
                    Versions = new List<NuGetPackageVersion>()
                };
                _context.Packages.Add(package);
            }

            // 创建新的版本对象以避免跟踪冲突
            var newVersion = new NuGetPackageVersion
            {
                PackageId = packageVersion.PackageId,
                Version = packageVersion.Version,
                Published = packageVersion.Published,
                DownloadUrl = packageVersion.DownloadUrl,
                FileSize = packageVersion.FileSize,
                Dependencies = packageVersion.Dependencies,
                ReleaseNotes = packageVersion.ReleaseNotes,
                IsPrerelease = packageVersion.IsPrerelease,
                LocalPath = packageVersion.LocalPath,
                IsDownloaded = packageVersion.IsDownloaded,
                CreatedTime = DateTime.Now,
                Listed = true
            };

            _context.PackageVersions.Add(newVersion);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"添加包版本失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 软删除包版本（设置为未列出）
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <returns>是否成功</returns>
    public async Task<bool> UnlistPackageVersionAsync(string packageId, string version)
    {
        try
        {
            var packageVersion = await _context.PackageVersions
                .FirstOrDefaultAsync(v => v.PackageId == packageId && v.Version == version);

            if (packageVersion != null)
            {
                packageVersion.Listed = false;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"取消列出包版本失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 重新列出包版本
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <returns>是否成功</returns>
    public async Task<bool> RelistPackageVersionAsync(string packageId, string version)
    {
        try
        {
            var packageVersion = await _context.PackageVersions
                .FirstOrDefaultAsync(v => v.PackageId == packageId && v.Version == version);

            if (packageVersion != null)
            {
                packageVersion.Listed = true;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"重新列出包版本失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 增加包下载计数
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <param name="version">版本号</param>
    /// <returns>是否成功</returns>
    public async Task<bool> IncrementDownloadCountAsync(string packageId, string version)
    {
        try
        {
            var package = await _context.Packages
                .FirstOrDefaultAsync(p => p.Id == packageId);

            if (package != null)
            {
                package.DownloadCount++;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"增加下载计数失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取已列出的包版本
    /// </summary>
    /// <param name="packageId">包ID</param>
    /// <returns>版本列表</returns>
    public async Task<IEnumerable<string>> GetListedPackageVersionsAsync(string packageId)
    {
        try
        {
            return await _context.PackageVersions
                .Where(v => v.PackageId == packageId && v.Listed)
                .OrderByDescending(v => v.Published)
                .Select(v => v.Version)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取已列出包版本失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 更新包版本信息
    /// </summary>
    /// <param name="existingPackage">现有包</param>
    /// <param name="newVersions">新版本列表</param>
    private async Task UpdatePackageVersionsAsync(NuGetPackage existingPackage, ICollection<NuGetPackageVersion> newVersions)
    {
        foreach (var newVersion in newVersions)
        {
            // 检查传入的版本对象是否被跟踪，如果是则分离
            var trackedVersionEntity = _context.Entry(newVersion);
            if (trackedVersionEntity.State != EntityState.Detached)
            {
                trackedVersionEntity.State = EntityState.Detached;
            }
            
            // 检查数据库中是否已存在该包的该版本
            var existingVersion = await _context.PackageVersions
                .FirstOrDefaultAsync(v => v.PackageId == existingPackage.Id && v.Version == newVersion.Version);

            if (existingVersion == null)
            {
                // 添加新版本时创建全新对象，确保不被EF跟踪
                var versionToAdd = new NuGetPackageVersion();
                
                // 手动设置所有属性，确保Id为默认值
                versionToAdd.PackageId = existingPackage.Id;
                versionToAdd.Version = newVersion.Version;
                versionToAdd.Published = newVersion.Published;
                versionToAdd.DownloadUrl = newVersion.DownloadUrl;
                versionToAdd.FileSize = newVersion.FileSize;
                versionToAdd.Dependencies = newVersion.Dependencies;
                versionToAdd.ReleaseNotes = newVersion.ReleaseNotes;
                versionToAdd.IsPrerelease = newVersion.IsPrerelease;
                versionToAdd.LocalPath = newVersion.LocalPath;
                versionToAdd.IsDownloaded = newVersion.IsDownloaded;
                versionToAdd.Listed = newVersion.Listed;
                versionToAdd.CreatedTime = DateTime.Now;
                
                // 使用DbContext.Add确保标记为新增
                _context.PackageVersions.Add(versionToAdd);
            }
            else
            {
                // 更新现有版本
                existingVersion.Published = newVersion.Published;
                existingVersion.DownloadUrl = newVersion.DownloadUrl;
                existingVersion.FileSize = newVersion.FileSize;
                existingVersion.Dependencies = newVersion.Dependencies;
                existingVersion.ReleaseNotes = newVersion.ReleaseNotes;
                existingVersion.IsPrerelease = newVersion.IsPrerelease;
                existingVersion.Listed = newVersion.Listed;
                
                // 保留本地信息
                if (!string.IsNullOrEmpty(existingVersion.LocalPath))
                {
                    var fullPath = GetFullPath(existingVersion.LocalPath);
                    existingVersion.IsDownloaded = File.Exists(fullPath);
                }
            }
        }
    }

    /// <summary>
    /// 初始化数据库
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        try
        {
            await _context.EnsureDatabaseCreatedAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"初始化数据库失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _context?.Dispose();
    }

    #endregion
}