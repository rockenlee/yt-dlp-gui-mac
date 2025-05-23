# yt-dlp GUI for macOS

这是一个yt-dlp的图形用户界面，用于从各种网站下载视频。该项目是从Windows版本迁移到macOS平台的。

## 功能

- 从各种网站下载视频
- 选择不同的视频和音频格式
- 下载字幕
- 监控剪贴板自动分析URL
- 自定义下载配置

## 依赖项

- [yt-dlp](https://github.com/yt-dlp/yt-dlp)
- [FFmpeg](https://ffmpeg.org/)
- [aria2](https://aria2.github.io/)

## 构建要求

- .NET 6.0 SDK
- Visual Studio for Mac 或 JetBrains Rider

## 安装步骤

1. 安装.NET 6.0 SDK：

```bash
brew install --cask dotnet-sdk
```

2. 安装依赖项：

```bash
brew install yt-dlp ffmpeg aria2
```

3. 克隆仓库：

```bash
git clone https://github.com/yourusername/yt-dlp-gui-mac.git
cd yt-dlp-gui-mac
```

4. 构建项目：

```bash
dotnet build src/yt-dlp-gui-mac.csproj
```

5. 运行应用程序：

```bash
dotnet run --project src/yt-dlp-gui-mac.csproj
```

## 使用方法

1. 将视频URL粘贴到输入框中
2. 点击"分析"按钮
3. 选择所需的格式
4. 选择保存位置
5. 点击"下载"按钮

## 配置

应用程序会在首次运行时创建配置文件。您可以在设置选项卡中修改配置。

## 许可证

与原始项目相同的许可证。

## 致谢

- 原始Windows版本的开发者
- [Avalonia UI](https://avaloniaui.net/) 团队
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) 团队
