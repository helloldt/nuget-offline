using Microsoft.EntityFrameworkCore;
using System.IO;
using Wpf_nuget.Models;

namespace Wpf_nuget.Data;

/// <summary>
/// NuGet数据库上下文
/// </summary>
public class NuGetDbContext : DbContext
{
    /// <summary>
    /// NuGet包表
    /// </summary>
    public DbSet<NuGetPackage> Packages { get; set; }

    /// <summary>
    /// NuGet包版本表
    /// </summary>
    public DbSet<NuGetPackageVersion> PackageVersions { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">数据库选项</param>
    public NuGetDbContext(DbContextOptions<NuGetDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public NuGetDbContext()
    {
    }

    /// <summary>
    /// 配置数据库连接
    /// </summary>
    /// <param name="optionsBuilder">选项构建器</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = GetDatabasePath();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    /// <summary>
    /// 配置模型
    /// </summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置NuGetPackage实体
        modelBuilder.Entity<NuGetPackage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.Title).HasMaxLength(512);
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.Authors).HasMaxLength(512);
            entity.Property(e => e.Tags).HasMaxLength(1000);
            entity.Property(e => e.ProjectUrl).HasMaxLength(512);
            entity.Property(e => e.IconUrl).HasMaxLength(512);
            entity.Property(e => e.LicenseUrl).HasMaxLength(512);
            entity.Property(e => e.Version).HasMaxLength(128);
            entity.Property(e => e.Summary).HasMaxLength(2000);
            entity.Property(e => e.Copyright).HasMaxLength(512);
            entity.Property(e => e.Language).HasMaxLength(50);
            entity.Property(e => e.ReleaseNotes).HasMaxLength(4000);
            entity.Property(e => e.MinClientVersion).HasMaxLength(50);

            entity.HasIndex(e => e.Id).IsUnique();
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.DownloadCount);
        });

        // 配置NuGetPackageVersion实体
        modelBuilder.Entity<NuGetPackageVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PackageId).HasMaxLength(256);
            entity.Property(e => e.Version).HasMaxLength(128);
            entity.Property(e => e.DownloadUrl).HasMaxLength(512);
            entity.Property(e => e.Dependencies).HasMaxLength(4000);
            entity.Property(e => e.ReleaseNotes).HasMaxLength(4000);
            entity.Property(e => e.LocalPath).HasMaxLength(512);

            entity.HasIndex(e => new { e.PackageId, e.Version }).IsUnique();
            entity.HasIndex(e => e.PackageId);
            entity.HasIndex(e => e.Version);
            entity.HasIndex(e => e.Published);
            entity.HasIndex(e => e.IsPrerelease);

            // 配置外键关系
            entity.HasOne(e => e.Package)
                  .WithMany(p => p.Versions)
                  .HasForeignKey(e => e.PackageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// 获取数据库文件路径
    /// </summary>
    /// <returns>数据库文件路径</returns>
    public static string GetDatabasePath()
    {
        // 使用程序运行目录
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(appDirectory, "nuget_packages.db");
    }

    /// <summary>
    /// 确保数据库已创建
    /// </summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        try
        {
            await Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"创建数据库失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 迁移数据库到最新版本
    /// </summary>
    /// <returns>异步任务</returns>
    public async Task MigrateDatabaseAsync()
    {
        try
        {
            await Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to migrate database: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 检查数据库连接
    /// </summary>
    /// <returns>是否连接成功</returns>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            return await Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取数据库信息
    /// </summary>
    /// <returns>数据库信息</returns>
    public async Task<object> GetDatabaseInfoAsync()
    {
        try
        {
            var dbPath = GetDatabasePath();
            var fileInfo = new FileInfo(dbPath);
            var packageCount = await Packages.CountAsync();
            var versionCount = await PackageVersions.CountAsync();
            var downloadedCount = await PackageVersions.CountAsync(v => v.IsDownloaded);

            return new
            {
                path = dbPath,
                exists = fileInfo.Exists,
                size = fileInfo.Exists ? fileInfo.Length : 0,
                packageCount,
                versionCount,
                downloadedCount,
                lastModified = fileInfo.Exists ? fileInfo.LastWriteTime : (DateTime?)null
            };
        }
        catch (Exception ex)
        {
            return new
            {
                error = ex.Message
            };
        }
    }

    /// <summary>
    /// 清理数据库（删除未下载的包版本记录）
    /// </summary>
    /// <returns>清理的记录数</returns>
    public async Task<int> CleanupDatabaseAsync()
    {
        try
        {
            // 删除未下载且创建时间超过7天的版本记录
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var versionsToDelete = await PackageVersions
                .Where(v => !v.IsDownloaded && v.CreatedTime < cutoffDate)
                .ToListAsync();

            if (versionsToDelete.Any())
            {
                PackageVersions.RemoveRange(versionsToDelete);
                await SaveChangesAsync();
            }

            // 删除没有版本的包记录
            var packagesWithoutVersions = await Packages
                .Where(p => !p.Versions.Any())
                .ToListAsync();

            if (packagesWithoutVersions.Any())
            {
                Packages.RemoveRange(packagesWithoutVersions);
                await SaveChangesAsync();
            }

            return versionsToDelete.Count + packagesWithoutVersions.Count;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to cleanup database: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 重建数据库（删除现有数据库并重新创建）
    /// </summary>
    /// <returns>异步任务</returns>
    public async Task RecreateDatabase()
    {
        try
        {
            // 删除现有数据库
            await Database.EnsureDeletedAsync();

            // 重新创建数据库
            await Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to recreate database: {ex.Message}", ex);
        }
    }
}