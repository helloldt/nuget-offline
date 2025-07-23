using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Wpf_nuget.Data;
using Wpf_nuget.Models;
using Wpf_nuget.Services;
using Wpf_nuget.ViewModels;

namespace Wpf_nuget;

/// <summary>
/// NuGet包管理工具主窗口
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    #region 私有字段

    private PackageDbService _dbService;
    private NuGetApiService _apiService;
    private NuGetDbContext _dbContext;

    private ObservableCollection<PackageViewModel> _packages;
    private ObservableCollection<VersionViewModel> _versions;
    private PackageViewModel? _selectedPackage;
    private VersionViewModel? _selectedVersion;

    private string _currentSearchQuery = "";
    private bool _isLoading = false;
    private bool _isLoadingMore = false;
    private bool _hasMoreData = true;
    private int _pageSize = 20;
    private int _loadedItemsCount = 0;
    
    // 取消令牌相关


    #endregion

    #region 属性

    public ObservableCollection<PackageViewModel> Packages
    {
        get => _packages;
        set
        {
            _packages = value;
            OnPropertyChanged(nameof(Packages));
        }
    }

    public ObservableCollection<VersionViewModel> Versions
    {
        get => _versions;
        set
        {
            _versions = value;
            OnPropertyChanged(nameof(Versions));
        }
    }

    public PackageViewModel? SelectedPackage
    {
        get => _selectedPackage;
        set
        {
            _selectedPackage = value;
            OnPropertyChanged(nameof(SelectedPackage));
        }
    }

    public VersionViewModel? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            _selectedVersion = value;
            OnPropertyChanged(nameof(SelectedVersion));
        }
    }

    #endregion

    #region 构造函数

    public MainWindow()
    {
        InitializeComponent();
        
        _packages = new ObservableCollection<PackageViewModel>();
        _versions = new ObservableCollection<VersionViewModel>();
        
        _dbContext = new NuGetDbContext();
        _dbService = new PackageDbService(_dbContext);
        _apiService = new NuGetApiService();

        DataContext = this;
        
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }
    
    private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // 清理缓存资源

            
            // 清理数据库连接
            _dbContext?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"关闭时出错: {ex.Message}");
        }
    }

    #endregion

    #region 事件处理

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatus("初始化数据库...");
            await InitializeDatabaseAsync();
            
            UpdateStatus("加载包列表...");
            await LoadPackagesAsync();
            
            await UpdateStatistics();
            UpdateStatus("就绪");
        }
        catch (Exception ex)
        {
            ShowError($"初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 初始化数据库，如果遇到列不存在的错误则重建数据库
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _dbContext.EnsureDatabaseCreatedAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("no such column") || ex.Message.Contains("Copyright"))
        {
            UpdateStatus("检测到数据库结构变更，正在重建数据库...");
            try
            {
                await _dbContext.RecreateDatabase();
                UpdateStatus("数据库重建完成");
            }
            catch (Exception recreateEx)
            {
                throw new Exception($"重建数据库失败: {recreateEx.Message}", recreateEx);
            }
        }
    }



    #endregion

    #region 工具栏事件

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatus("刷新中...");
            await LoadPackagesAsync();
            await UpdateStatistics();
            UpdateStatus("刷新完成");
        }
        catch (Exception ex)
        {
            ShowError($"刷新失败: {ex.Message}");
        }
    }



    private async void BtnCleanup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = MessageBox.Show("确定要清理数据库吗？这将删除未下载的包版本记录。", 
                "确认清理", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                UpdateStatus("清理数据库中...");
                var cleanedCount = await _dbContext.CleanupDatabaseAsync();
                
                await LoadPackagesAsync();
                await UpdateStatistics();
                
                UpdateStatus($"清理完成，删除了 {cleanedCount} 条记录");
            }
        }
        catch (Exception ex)
        {
            ShowError($"清理数据库失败: {ex.Message}");
        }
    }

    private async void BtnDbInfo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dbInfo = await _dbContext.GetDatabaseInfoAsync();
            var info = $"数据库信息:\n" +
                      $"路径: {dbInfo.GetType().GetProperty("path")?.GetValue(dbInfo)}\n" +
                      $"大小: {FormatFileSize((long)(dbInfo.GetType().GetProperty("size")?.GetValue(dbInfo) ?? 0))}\n" +
                      $"包数量: {dbInfo.GetType().GetProperty("packageCount")?.GetValue(dbInfo)}\n" +
                      $"版本数量: {dbInfo.GetType().GetProperty("versionCount")?.GetValue(dbInfo)}\n" +
                      $"已下载: {dbInfo.GetType().GetProperty("downloadedCount")?.GetValue(dbInfo)}";
            
            MessageBox.Show(info, "数据库信息", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"获取数据库信息失败: {ex.Message}");
        }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatus("导出中...");
            
            // 获取所有已下载的包版本信息
            var downloadedVersions = await _dbContext.PackageVersions
                .Include(v => v.Package)
                .Where(v => v.IsDownloaded)
                .OrderBy(v => v.Package.Id)
                .ThenBy(v => v.Version)
                .ToListAsync();

            if (!downloadedVersions.Any())
            {
                MessageBox.Show("没有已下载的包可以导出。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStatus("就绪");
                return;
            }

            // 选择保存位置
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt",
                FileName = "packages.txt",
                Title = "导出包信息"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var exportContent = new List<string>();
                exportContent.Add("# NuGet包导出文件");
                exportContent.Add($"# 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                exportContent.Add($"# 包数量: {downloadedVersions.Count}");
                exportContent.Add("");

                foreach (var version in downloadedVersions)
                {
                    var line = $"{version.Package.Id}|{version.Version}|{version.Package.Title}|{version.Package.Authors}|{version.Package.Description?.Replace("\n", " ").Replace("\r", " ")}|{version.LocalPath ?? ""}";
                    exportContent.Add(line);
                }

                await File.WriteAllLinesAsync(saveDialog.FileName, exportContent);
                
                UpdateStatus($"导出完成，共导出 {downloadedVersions.Count} 个包版本");
                MessageBox.Show($"成功导出 {downloadedVersions.Count} 个包版本到:\n{saveDialog.FileName}", 
                    "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                UpdateStatus("导出已取消");
            }
        }
        catch (Exception ex)
        {
            ShowError($"导出失败: {ex.Message}");
        }
    }

    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 选择导入文件
            var openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt",
                Title = "选择包信息文件"
            };

            if (openDialog.ShowDialog() != true)
            {
                return;
            }

            UpdateStatus("读取导入文件中...");
            
            var lines = await File.ReadAllLinesAsync(openDialog.FileName);
            var packageLines = lines.Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")).ToList();

            if (!packageLines.Any())
            {
                MessageBox.Show("导入文件中没有找到有效的包信息。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                UpdateStatus("就绪");
                return;
            }

            var result = MessageBox.Show($"将要导入 {packageLines.Count} 个包版本，这可能需要一些时间。\n确定要继续吗？", 
                "确认导入", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes)
            {
                UpdateStatus("导入已取消");
                return;
            }

            UpdateStatus("导入中...");
            int successCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            foreach (var line in packageLines)
            {
                try
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 2)
                    {
                        var packageId = parts[0].Trim();
                        var version = parts[1].Trim();
                        
                        UpdateStatus($"正在处理: {packageId} {version}...");
                        
                        // 检查是否已存在
                        var existingVersion = await _dbContext.PackageVersions
                            .FirstOrDefaultAsync(v => v.PackageId == packageId && v.Version == version);
                        
                        if (existingVersion?.IsDownloaded == true)
                        {
                            continue; // 已下载，跳过
                        }

                        try
                        {
                            // 从API获取包信息
                            var packageInfo = await _apiService.GetPackageAsync(packageId);
                            if (packageInfo != null)
                            {
                                // 添加或更新包信息
                                await _dbService.AddOrUpdatePackageAsync(packageInfo);
                                
                                // 获取版本信息
                                var versionInfo = await _apiService.GetPackageVersionAsync(packageId, version);
                                if (versionInfo != null)
                                {
                                    // 下载包
                                    try
                                    {
                                        var downloadedFilePath = await _apiService.DownloadPackageAsync(packageId, version);
                                        // 更新数据库中的下载状态
                                        await _dbService.UpdateVersionDownloadStatusAsync(packageId, version, true, downloadedFilePath);
                                        successCount++;
                                    }
                                    catch (Exception downloadEx)
                                    {
                                        errors.Add($"{packageId} {version}: 下载失败 - {downloadEx.Message}");
                                        errorCount++;
                                    }
                                }
                                else
                                {
                                    errors.Add($"{packageId} {version}: 获取版本信息失败");
                                    errorCount++;
                                }
                            }
                            else
                            {
                                errors.Add($"{packageId}: 获取包信息失败");
                                errorCount++;
                            }
                        }
                        catch (Exception downloadEx)
                        {
                            // 下载失败，记录错误但继续处理
                            errors.Add($"{packageId} {version}: 处理失败 - {downloadEx.Message}");
                            errorCount++;
                            System.Diagnostics.Debug.WriteLine($"处理包 {packageId} {version} 失败: {downloadEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"处理行 '{line}' 时出错: {ex.Message}");
                    errorCount++;
                }
            }

            // 刷新界面
            await LoadPackagesAsync();
            await UpdateStatistics();

            var message = $"导入完成！\n成功: {successCount}\n失败: {errorCount}";
            if (errors.Any())
            {
                message += $"\n\n错误详情:\n{string.Join("\n", errors.Take(10))}";
                if (errors.Count > 10)
                {
                    message += $"\n... 还有 {errors.Count - 10} 个错误";
                }
            }

            MessageBox.Show(message, "导入结果", MessageBoxButton.OK, 
                errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            
            UpdateStatus($"导入完成 - 成功: {successCount}, 失败: {errorCount}");
        }
        catch (Exception ex)
        {
            ShowError($"导入失败: {ex.Message}");
        }
    }

    #endregion

    #region 搜索事件

    private async void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SearchPackagesAsync();
        }
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        await SearchPackagesAsync();
    }
    
    private async void ChkPrerelease_Changed(object sender, RoutedEventArgs e)
    {
        // 预发布版本选择改变时，清理缓存并重新加载
        await ClearCacheAsync();
        await LoadPackagesAsync();
    }
    
    private async void CmbSearchSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 搜索源改变时，清理缓存并重新加载
        await ClearCacheAsync();
        if (_dbContext!=null)
        {
            await LoadPackagesAsync();
        }
     
    }

    #endregion

    #region 滚动加载事件

    private async void LvPackages_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isLoadingMore || !_hasMoreData) return;

        var scrollViewer = e.OriginalSource as ScrollViewer;
        if (scrollViewer == null) return;

        // 当滚动到底部附近时加载更多数据
        if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 100)
        {
            await LoadMorePackagesAsync();
        }
    }

    #endregion

    #region 列表选择事件

    private async void LvPackages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lvPackages.SelectedItem is PackageViewModel package)
        {
            SelectedPackage = package;
            await LoadPackageDetailsAsync(package);
            await LoadPackageVersionsAsync(package.Id);
        }
    }

    private void LvVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lvVersions.SelectedItem is VersionViewModel version)
        {
            SelectedVersion = version;
            UpdateVersionButtons();
        }
    }

    #endregion

    #region 版本管理事件

    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPackage == null || SelectedVersion == null)
            return;

        try
        {
            UpdateStatus($"下载 {SelectedPackage.Id} v{SelectedVersion.Version}...");
            
            // 确保包信息存在于数据库中
            await _dbService.AddOrUpdatePackageAsync(SelectedPackage.ToNuGetPackage());
            
            // 确保版本信息存在于数据库中
            var packageVersion = SelectedVersion.ToNuGetPackageVersion();
            packageVersion.PackageId = SelectedPackage.Id;
            await _dbService.AddPackageVersionAsync(packageVersion);
            
            var downloadPath = await _apiService.DownloadPackageAsync(SelectedPackage.Id, SelectedVersion.Version);
            var fileSize = _apiService.GetPackageFileSize(downloadPath);
            
            await _dbService.UpdateVersionLocalPathAsync(SelectedPackage.Id, SelectedVersion.Version, downloadPath, fileSize);
            
            // 刷新版本列表
            await LoadPackageVersionsAsync(SelectedPackage.Id);
            await UpdateStatistics();
            
            UpdateStatus($"下载完成: {downloadPath}");
        }
        catch (Exception ex)
        {
            // 构建详细的错误信息
            var errorDetails = $"下载失败: {ex.Message}";
            
            if (ex.InnerException != null)
            {
                errorDetails += $"\n\n内部异常: {ex.InnerException.Message}";
                
                if (ex.InnerException.InnerException != null)
                {
                    errorDetails += $"\n\n深层异常: {ex.InnerException.InnerException.Message}";
                }
            }
            
            // 显示详细错误信息
            MessageBox.Show(errorDetails, "下载错误详情", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // 同时输出到调试控制台
            System.Diagnostics.Debug.WriteLine($"下载错误详情:\n{errorDetails}\n\n堆栈跟踪:\n{ex.StackTrace}");
        }
    }

    private async void BtnSync_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPackage == null)
            return;

        try
        {
            UpdateStatus($"同步 {SelectedPackage.Id}...");
            
            var onlineResults = await _apiService.SearchPackagesAsync(SelectedPackage.Id, 0, 1, true);
            var onlinePackage = onlineResults.FirstOrDefault(p => p.Id.Equals(SelectedPackage.Id, StringComparison.OrdinalIgnoreCase));
            
            if (onlinePackage != null)
            {
                await _dbService.AddOrUpdatePackageAsync(onlinePackage);
                await LoadPackagesAsync();
                await LoadPackageVersionsAsync(SelectedPackage.Id);
                UpdateStatus("同步完成");
            }
            else
            {
                ShowError("未找到在线包信息");
            }
        }
        catch (Exception ex)
        {
            ShowError($"同步失败: {ex.Message}");
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedVersion?.LocalPath != null)
        {
            var fullPath = GetFullPath(SelectedVersion.LocalPath);
            if (File.Exists(fullPath))
            {
                var folder = Path.GetDirectoryName(fullPath);
                if (Directory.Exists(folder))
                {
                    Process.Start("explorer.exe", folder);
                }
            }
            else
            {
                ShowError("文件不存在或未下载");
            }
        }
        else
        {
            ShowError("文件不存在或未下载");
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

    #region 包详情事件

    private void TxtPackageProjectUrl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SelectedPackage?.ProjectUrl != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = SelectedPackage.ProjectUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError($"打开链接失败: {ex.Message}");
            }
        }
    }

    #endregion

    #region 私有方法

    private async Task LoadPackagesAsync()
    {
        if (_isLoading) return;
        
        _isLoading = true;
        _loadedItemsCount = 0;
        _hasMoreData = true;
        UpdateStatus("正在加载包列表...");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========== 开始加载包列表 ==========");
        
        try
        {
            // 清空现有数据
            Packages.Clear();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 清空现有包列表");
            
            // 加载第一页数据
            await LoadMorePackagesAsync();
        }
        catch (Exception ex)
        {
            ShowError($"加载包列表失败: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 加载包列表失败: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========== 包列表加载结束 ==========");
        }
    }

    /// <summary>
    /// 加载更多包数据（滚动加载）
    /// </summary>
    private async Task LoadMorePackagesAsync()
    {
        if (_isLoadingMore || !_hasMoreData) return;
        
        _isLoadingMore = true;
        txtLoadingStatus.Text = "正在加载更多...";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 开始加载更多包数据，当前已加载: {_loadedItemsCount} 个");
        
        try
        {
            var packages = await LoadPageFromSourceAsync(_loadedItemsCount);
            var packageList = packages.ToList();
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 本次获取到 {packageList.Count} 个包");
            
            if (packageList.Count == 0 || packageList.Count < _pageSize)
            {
                _hasMoreData = false;
                txtLoadingStatus.Text = packageList.Count == 0 ? "没有更多数据" : $"已加载全部 {_loadedItemsCount + packageList.Count} 个包";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 数据加载完成，总计: {_loadedItemsCount + packageList.Count} 个包");
            }
            else
            {
                txtLoadingStatus.Text = $"已加载 {_loadedItemsCount + packageList.Count} 个包";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 还有更多数据可加载");
            }
            
            // 添加到现有列表
            foreach (var package in packageList)
            {
                Packages.Add(package);
            }
            
            _loadedItemsCount += packageList.Count;
            
            if (packageList.Count > 0)
            {
                UpdateStatus($"加载了 {packageList.Count} 个包，总计 {_loadedItemsCount} 个");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 界面更新完成，总计显示: {_loadedItemsCount} 个包");
            }
        }
        catch (Exception ex)
        {
            ShowError($"加载更多包失败: {ex.Message}");
            txtLoadingStatus.Text = "加载失败";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 加载失败: {ex.Message}");
        }
        finally
        {
            _isLoadingMore = false;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 加载操作结束");
        }
    }
    
    #region 缓存相关方法
    
    /// <summary>
    /// 从数据源加载页面数据
    /// </summary>
    private async Task<IEnumerable<PackageViewModel>> LoadPageFromSourceAsync(int skip = 0)
    {
        var prerelease = chkPrerelease.IsChecked ?? false;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 开始从数据源加载数据，跳过: {skip}，页大小: {_pageSize}，包含预发布: {prerelease}");
        
        IEnumerable<NuGetPackage> packages;
        
        if (string.IsNullOrEmpty(_currentSearchQuery))
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 加载所有本地包数据");
            packages = await _dbService.SearchPackagesAsync("", skip, _pageSize, prerelease);
            if (skip == 0) // 只在第一次加载时更新状态
            {
                var totalCount = await _dbService.GetPackageCountAsync();
                UpdateStatus($"显示本地包，共 {totalCount} 个");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 本地包总数: {totalCount}");
            }
        }
        else
        {
            var searchSource = cmbSearchSource.SelectedIndex;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 搜索关键词: '{_currentSearchQuery}'，搜索源: {searchSource}");
            switch (searchSource)
            {
                case 1: // 仅本地
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 从本地数据库搜索");
                    packages = await _dbService.SearchPackagesAsync(_currentSearchQuery, skip, _pageSize, prerelease);
                    if (skip == 0)
                    {
                        UpdateSearchStatus("本地");
                    }
                    break;
                case 2: // 仅在线
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 从在线API搜索");
                    packages = await _apiService.SearchPackagesAsync(_currentSearchQuery, skip, _pageSize, prerelease);
                    if (skip == 0)
                    {
                        UpdateSearchStatus("在线");
                    }
                    break;
                default: // 全部
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 从本地和在线搜索");
                    var localResults = await _dbService.SearchPackagesAsync(_currentSearchQuery, skip, _pageSize, prerelease);
                    var localCount = localResults.Count();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 本地搜索结果: {localCount} 个");
                    
                    if (localCount < _pageSize)
                    {
                        var remainingTake = _pageSize - localCount;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 本地结果不足，从在线获取额外 {remainingTake} 个");
                        var onlineResults = await _apiService.SearchPackagesAsync(_currentSearchQuery, skip + localCount, remainingTake, prerelease);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 在线搜索结果: {onlineResults.Count()} 个");
                        packages = localResults.Concat(onlineResults).GroupBy(p => p.Id).Select(g => g.First());
                    }
                    else
                    {
                        packages = localResults;
                    }
                    
                    if (skip == 0)
                    {
                        UpdateSearchStatus("全部");
                    }
                    break;
            }
        }
        
        var result = packages.Select(p => new PackageViewModel(p));
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 数据源加载完成，返回 {result.Count()} 个包");
        return result;
    }
    
    /// <summary>
    /// 清理缓存（保留用于搜索条件变化时清理数据）
    /// </summary>
    private async Task ClearCacheAsync()
    {
        // 在滚动加载模式下，主要用于重置加载状态
        _loadedItemsCount = 0;
        _hasMoreData = true;

    }
    
    #endregion

    private async Task SearchPackagesAsync()
    {
        _currentSearchQuery = txtSearch.Text.Trim();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========== 开始搜索包 ==========");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 搜索关键词: '{_currentSearchQuery}'");
        
        // 清理状态，因为搜索条件已改变
        await ClearCacheAsync();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 清理缓存状态完成");
        
        await LoadPackagesAsync();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========== 搜索包完成 ==========");
    }

    private async Task LoadPackageDetailsAsync(PackageViewModel package)
    {
        try
        {
            txtPackageId.Text = package.Id;
            txtPackageTitle.Text = package.Title;
            txtPackageAuthors.Text = package.Authors;
            txtPackageDescription.Text = package.Description;
            txtPackageDownloads.Text = package.DownloadCount.ToString("N0");
            txtPackageProjectUrl.Text = package.ProjectUrl;
            txtPackageTags.Text = package.Tags;
        }
        catch (Exception ex)
        {
            ShowError($"加载包详情失败: {ex.Message}");
        }
    }

    private async Task LoadPackageVersionsAsync(string packageId)
    {
        try
        {
            Versions.Clear();
            
            // 优先从本地获取
            var localVersions = await _dbService.GetPackageVersionsAsync(packageId);
            if (localVersions.Any())
            {
                foreach (var version in localVersions.OrderByDescending(v => v.Published))
                {
                    Versions.Add(new VersionViewModel(version));
                }
            }
            else
            {
                // 从在线获取
                var onlineVersions = await _apiService.GetPackageVersionsAsync(packageId);
                foreach (var version in onlineVersions.OrderByDescending(v => v.Published))
                {
                    Versions.Add(new VersionViewModel(version));
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"加载版本列表失败: {ex.Message}");
        }
    }


    
    private void UpdateSearchStatus(string source)
    {
        var statusMessage = $"搜索 '{_currentSearchQuery}' ({source})：";
        
        if (_loadedItemsCount == 0)
        {
            statusMessage += "未找到匹配的包";
        }
        else
        {
            statusMessage += $"已加载 {_loadedItemsCount} 个包";
            if (_hasMoreData)
            {
                statusMessage += "，向下滚动加载更多";
            }
        }
        
        UpdateStatus(statusMessage);
    }

    private void UpdateVersionButtons()
    {
        var hasSelection = SelectedVersion != null;
        btnDownload.IsEnabled = hasSelection && !SelectedVersion?.IsDownloaded == true;
        btnSync.IsEnabled = SelectedPackage != null;
        btnOpenFolder.IsEnabled = hasSelection && SelectedVersion?.IsDownloaded == true;
    }

    private async Task UpdateStatistics()
    {
        try
        {
            var packageCount = await _dbService.GetPackageCountAsync();
            var downloadedCount = await _dbService.GetDownloadedPackageCountAsync();
            
            txtPackageCount.Text = $"包数量: {packageCount}";
            txtDownloadedCount.Text = $"已下载: {downloadedCount}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新统计信息失败: {ex.Message}");
        }
    }

    private void UpdateStatus(string message)
    {
        txtStatus.Text = message;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        UpdateStatus($"错误: {message}");
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}