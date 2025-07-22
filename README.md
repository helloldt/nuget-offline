<img width="1280" height="762" alt="main" src="https://github.com/user-attachments/assets/7d86cb58-24f2-4f4d-b99f-f67ede07afc5" />



# NuGet Package Manager / NuGet 包管理器

[English](#english) | [中文](#chinese)

---


### Overview

A modern WPF-based NuGet package management tool that provides an intuitive interface for searching, downloading, and managing NuGet packages. This application combines online package discovery with local package management capabilities.

### Features

- **🔍 Package Search**: Search NuGet packages from the official NuGet repository
- **📦 Package Management**: Download and manage packages locally
- **💾 Local Storage**: SQLite database for package metadata and version tracking
- **🎯 Version Control**: Support for multiple package versions and prerelease packages
- **📁 File Management**: Automatic package file organization and path management
- **🔄 Sync Capabilities**: Synchronize online and local package information
- **🎨 Modern UI**: Clean and intuitive WPF interface with modern styling

### Technology Stack

- **Framework**: .NET 8.0 (Windows)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Database**: SQLite with Entity Framework Core 8.0
- **NuGet Integration**: NuGet.Protocol 6.14.0, NuGet.Packaging 6.14.0
- **JSON Processing**: Newtonsoft.Json 13.0.3
- **Architecture**: MVVM Pattern with ViewModels

### Project Structure

```
Wpf_nuget/
├── Data/
│   └── NuGetDbContext.cs          # Entity Framework DbContext
├── Models/
│   └── NuGetPackage.cs             # Data models for packages and versions
├── Services/
│   ├── NuGetApiService.cs          # NuGet API integration service
│   └── PackageDbService.cs         # Database operations service
├── ViewModels/
│   ├── PackageViewModel.cs         # Package view model
│   └── VersionViewModel.cs         # Version view model
├── MainWindow.xaml                 # Main UI layout
├── MainWindow.xaml.cs              # Main window code-behind
└── App.xaml                        # Application configuration
```

### Key Components

#### 1. NuGetApiService
- Handles communication with NuGet.org API
- Package search and metadata retrieval
- Package download functionality
- Relative path management for portability

#### 2. PackageDbService
- SQLite database operations
- Package and version data persistence
- Local package tracking and status management

#### 3. Data Models
- **NuGetPackage**: Core package information
- **NuGetPackageVersion**: Version-specific details
- Entity Framework relationships and constraints

#### 4. ViewModels
- MVVM pattern implementation
- Data binding for UI components
- Property change notifications

### Installation & Setup

1. **Prerequisites**:
   - .NET 8.0 SDK or later
   - Windows 10/11
   - Visual Studio 2022 (recommended)

2. **Clone and Build**:
   ```bash
   git clone <repository-url>
   cd Wpf_nuget
   dotnet restore
   dotnet build
   ```

3. **Run**:
   ```bash
   dotnet run
   ```

### Usage

1. **Search Packages**: Use the search bar to find NuGet packages
2. **Filter Options**: Choose between "All", "Local Only", or "Online Only"
3. **View Details**: Select a package to view its versions and details
4. **Download**: Click download to save packages locally
5. **Manage**: View downloaded packages and their file information

### Database Schema

The application uses SQLite with the following main entities:

- **Packages**: Core package information (Id, Title, Description, Authors, etc.)
- **PackageVersions**: Version-specific data (Version, Published date, Dependencies, etc.)

### Path Management

The application implements a robust path management system:
- **Relative Paths**: Packages are stored using relative paths for portability
- **Runtime Resolution**: Full paths are resolved at runtime based on application location
- **Migration Support**: Applications can be moved between directories/computers safely


---



### 概述

一个基于 WPF 的现代化 NuGet 包管理工具，提供直观的界面用于搜索、下载和管理 NuGet 包。该应用程序结合了在线包发现和本地包管理功能。

### 功能特性

- **🔍 包搜索**: 从官方 NuGet 仓库搜索包
- **📦 包管理**: 本地下载和管理包
- **💾 本地存储**: 使用 SQLite 数据库存储包元数据和版本跟踪
- **🎯 版本控制**: 支持多个包版本和预发布包
- **📁 文件管理**: 自动包文件组织和路径管理
- **🔄 同步功能**: 同步在线和本地包信息
- **🎨 现代界面**: 简洁直观的 WPF 界面，采用现代化样式

### 技术栈

- **框架**: .NET 8.0 (Windows)
- **UI 框架**: WPF (Windows Presentation Foundation)
- **数据库**: SQLite 配合 Entity Framework Core 8.0
- **NuGet 集成**: NuGet.Protocol 6.14.0, NuGet.Packaging 6.14.0
- **JSON 处理**: Newtonsoft.Json 13.0.3
- **架构**: MVVM 模式配合 ViewModels

### 项目结构

```
Wpf_nuget/
├── Data/
│   └── NuGetDbContext.cs          # Entity Framework 数据库上下文
├── Models/
│   └── NuGetPackage.cs             # 包和版本的数据模型
├── Services/
│   ├── NuGetApiService.cs          # NuGet API 集成服务
│   └── PackageDbService.cs         # 数据库操作服务
├── ViewModels/
│   ├── PackageViewModel.cs         # 包视图模型
│   └── VersionViewModel.cs         # 版本视图模型
├── MainWindow.xaml                 # 主界面布局
├── MainWindow.xaml.cs              # 主窗口代码隐藏
└── App.xaml                        # 应用程序配置
```

### 核心组件

#### 1. NuGetApiService
- 处理与 NuGet.org API 的通信
- 包搜索和元数据检索
- 包下载功能
- 相对路径管理以提高可移植性

#### 2. PackageDbService
- SQLite 数据库操作
- 包和版本数据持久化
- 本地包跟踪和状态管理

#### 3. 数据模型
- **NuGetPackage**: 核心包信息
- **NuGetPackageVersion**: 版本特定详细信息
- Entity Framework 关系和约束

#### 4. ViewModels
- MVVM 模式实现
- UI 组件数据绑定
- 属性更改通知

### 安装和设置

1. **先决条件**:
   - .NET 8.0 SDK 或更高版本
   - Windows 10/11
   - Visual Studio 2022 (推荐)

2. **克隆和构建**:
   ```bash
   git clone <repository-url>
   cd Wpf_nuget
   dotnet restore
   dotnet build
   ```

3. **运行**:
   ```bash
   dotnet run
   ```

### 使用方法

1. **搜索包**: 使用搜索栏查找 NuGet 包
2. **筛选选项**: 在"全部"、"仅本地"或"仅在线"之间选择
3. **查看详情**: 选择一个包以查看其版本和详细信息
4. **下载**: 点击下载将包保存到本地
5. **管理**: 查看已下载的包及其文件信息

### 数据库架构

应用程序使用 SQLite，包含以下主要实体：

- **Packages**: 核心包信息（Id、标题、描述、作者等）
- **PackageVersions**: 版本特定数据（版本号、发布日期、依赖项等）

### 路径管理

应用程序实现了强大的路径管理系统：
- **相对路径**: 使用相对路径存储包以提高可移植性
- **运行时解析**: 基于应用程序位置在运行时解析完整路径
- **迁移支持**: 应用程序可以安全地在目录/计算机之间移动


