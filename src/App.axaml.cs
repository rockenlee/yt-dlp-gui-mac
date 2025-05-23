using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Linq;
using yt_dlp_gui_mac.Views;

namespace yt_dlp_gui_mac
{
    public partial class App : Application
    {
        public static string CurrentVersion = "2023.03.28";
        public static string AppExe;
        public static string AppPath;
        public static string AppName;
        public static Models.Lang Lang { get; set; } = new();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Console.WriteLine("Framework initialization completed");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Console.WriteLine("Desktop application lifetime detected");
                LoadPath();

                Console.WriteLine($"AppPath: {AppPath}");
                Console.WriteLine($"AppName: {AppName}");

                var langPath = Path(Folders.root, AppName + ".lang");
                Console.WriteLine($"Lang path: {langPath}");

                if (File.Exists(langPath))
                {
                    Console.WriteLine("Loading language file");
                    Lang = Libs.Yaml.Open<Models.Lang>(langPath);
                }

                Console.WriteLine("Creating main window");
                desktop.MainWindow = new MainWindow();
                Console.WriteLine("Main window created");
            }
            else
            {
                Console.WriteLine("Not a desktop application lifetime");
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void LoadPath()
        {
            // 使用AppContext.BaseDirectory获取应用程序目录
            AppExe = Environment.ProcessPath ?? AppContext.BaseDirectory;
            AppPath = AppContext.BaseDirectory;

            // 如果AppPath为空或不存在，尝试使用其他方法
            if (string.IsNullOrEmpty(AppPath) || !Directory.Exists(AppPath))
            {
                AppPath = System.IO.Path.GetDirectoryName(AppExe);

                // 如果仍然为空或不存在，使用当前目录
                if (string.IsNullOrEmpty(AppPath) || !Directory.Exists(AppPath))
                {
                    AppPath = Directory.GetCurrentDirectory();
                    Console.WriteLine($"Using current directory as fallback: {AppPath}");
                }
            }

            AppName = System.IO.Path.GetFileNameWithoutExtension(AppExe);

            Console.WriteLine($"LoadPath: AppExe={AppExe}, AppPath={AppPath}, AppName={AppName}");

            // 检查deps目录
            string depsDir = System.IO.Path.Combine(AppPath, "deps");
            if (Directory.Exists(depsDir))
            {
                Console.WriteLine($"Found deps directory: {depsDir}");

                // 检查yt-dlp
                string ytdlpPath = System.IO.Path.Combine(depsDir, "yt-dlp");
                if (File.Exists(ytdlpPath))
                {
                    Console.WriteLine($"Found yt-dlp in deps directory: {ytdlpPath}");
                    Wrappers.DLP.Path_DLP = ytdlpPath;
                }

                // 检查ffmpeg
                string ffmpegPath = System.IO.Path.Combine(depsDir, "ffmpeg");
                if (File.Exists(ffmpegPath))
                {
                    Console.WriteLine($"Found ffmpeg in deps directory: {ffmpegPath}");
                    Wrappers.DLP.Path_FFMPEG = ffmpegPath;
                }
            }
        }

        public static string Path(Folders type, params string[] pathpart)
        {
            var parmas = new System.Collections.Generic.List<string> { AppPath };

            parmas.AddRange(type switch
            {
                Folders.root => Array.Empty<string>(),
                Folders.bin => new[] { "bin" },
                Folders.configs => new[] { "configs" },
                Folders.temp => new[] { "temp" },
                _ => throw new NotImplementedException(),
            });

            parmas.AddRange(pathpart);

            try
            {
                return System.IO.Path.Combine(parmas.ToArray());
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public enum Folders
        {
            root, bin, configs, temp
        }
    }
}
