using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Skia;
using System;

namespace yt_dlp_gui_mac
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Fix for "error messaging the mach port for IMKCFRunLoopWakeUpReliable"
            Environment.SetEnvironmentVariable("AVALONIA_INPUT_METHOD", "0");
            
            BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            Console.WriteLine("Building Avalonia App");

            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .With(new SkiaOptions { MaxGpuResourceSizeBytes = 8096000 });
        }
    }
}
