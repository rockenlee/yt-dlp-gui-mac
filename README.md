# yt-dlp GUI for macOS

*[🇺🇸 English](#english) | [🇨🇳 中文](#中文)*

---
<img width="974" alt="image" src="https://github.com/user-attachments/assets/270da5cf-e256-4e6a-8520-11c3f2042b29" />



## English

### Overview

A graphical user interface for yt-dlp that allows downloading videos from various websites. This project is a macOS port of the original Windows version.

### Features

- Download videos from various websites
- Select different video and audio formats
- Download subtitles
- Clipboard monitoring for automatic URL analysis
- Customizable download configurations
- Modern and intuitive macOS interface

### Dependencies

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) - The core video downloader
- [FFmpeg](https://ffmpeg.org/) - Media processing framework
- [aria2](https://aria2.github.io/) - Download accelerator

### Build Requirements

- .NET 9.0 SDK
- macOS 10.15 (Catalina) or later
- Visual Studio for Mac, JetBrains Rider, or Visual Studio Code

### Installation

1. **Install .NET 9.0 SDK:**

```bash
brew install --cask dotnet-sdk
```

2. **Install dependencies:**

```bash
brew install yt-dlp ffmpeg aria2
```

3. **Clone the repository:**

```bash
git clone https://github.com/rockenlee/yt-dlp-gui-mac.git
cd yt-dlp-gui-mac
```

4. **Build the project:**

```bash
dotnet build src/yt-dlp-gui-mac.csproj
```

5. **Run the application:**

```bash
dotnet run --project src/yt-dlp-gui-mac.csproj
```

### Usage

1. Paste a video URL into the input field
2. Click the "Analyze" button to fetch video information
3. Select your preferred video and audio formats
4. Choose the download location
5. Click the "Download" button to start downloading

### Configuration

The application creates configuration files on first run. You can modify settings through the Settings tab in the application.

### Building for Distribution

To create a standalone application bundle:

```bash
dotnet publish src/yt-dlp-gui-mac.csproj -c Release -r osx-x64 --self-contained
```

### Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### Acknowledgments

- Original Windows version developers
- [Avalonia UI](https://avaloniaui.net/) team for the cross-platform UI framework
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) team for the excellent video downloader

---

## 中文

### 概述

这是一个yt-dlp的图形用户界面，用于从各种网站下载视频。该项目是从Windows版本迁移到macOS平台的。

### 功能特性

- 从各种网站下载视频
- 选择不同的视频和音频格式
- 下载字幕
- 监控剪贴板自动分析URL
- 自定义下载配置
- 现代化的macOS原生界面

### 依赖项

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) - 核心视频下载器
- [FFmpeg](https://ffmpeg.org/) - 媒体处理框架
- [aria2](https://aria2.github.io/) - 下载加速器

### 构建要求

- .NET 9.0 SDK
- macOS 10.15 (Catalina) 或更高版本
- Visual Studio for Mac、JetBrains Rider 或 Visual Studio Code

### 安装步骤

1. **安装.NET 9.0 SDK：**

```bash
brew install --cask dotnet-sdk
```

2. **安装依赖项：**

```bash
brew install yt-dlp ffmpeg aria2
```

3. **克隆仓库：**

```bash
git clone https://github.com/rockenlee/yt-dlp-gui-mac.git
cd yt-dlp-gui-mac
```

4. **构建项目：**

```bash
dotnet build src/yt-dlp-gui-mac.csproj
```

5. **运行应用程序：**

```bash
dotnet run --project src/yt-dlp-gui-mac.csproj
```

### 使用方法

1. 将视频URL粘贴到输入框中
2. 点击"分析"按钮获取视频信息
3. 选择所需的视频和音频格式
4. 选择保存位置
5. 点击"下载"按钮开始下载

### 配置

应用程序会在首次运行时创建配置文件。您可以在应用程序的设置选项卡中修改配置。

### 构建发布版本

创建独立的应用程序包：

```bash
dotnet publish src/yt-dlp-gui-mac.csproj -c Release -r osx-x64 --self-contained
```

### 贡献

1. Fork 这个仓库
2. 创建功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'Add some amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 创建 Pull Request

### 许可证

本项目采用 MIT 许可证 - 详情请参阅 [LICENSE](LICENSE) 文件。

### 致谢

- 原始Windows版本的开发者
- [Avalonia UI](https://avaloniaui.net/) 团队提供的跨平台UI框架
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) 团队提供的优秀视频下载器
