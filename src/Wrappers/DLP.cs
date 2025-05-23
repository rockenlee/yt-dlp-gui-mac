using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using yt_dlp_gui_mac.Models;

namespace yt_dlp_gui_mac.Wrappers
{
    public class DLP
    {
        public enum DLPType
        {
            yd_dlp, youtube_dl
        }

        static public DLPType Type { get; set; } = DLPType.yd_dlp;
        static public string Path_DLP { get; set; } = string.Empty;
        static public string Path_Aria2 { get; set; } = string.Empty;
        static public string Path_FFMPEG { get; set; } = string.Empty;

        public List<string> Files { get; set; } = new List<string>();
        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
        public string Url { get; set; } = string.Empty;
        public bool IsLive { get; set; } = false;
        public HashSet<DLPError> StdErr { get; set; } = new();

        private Process process = new();

        public enum DLPError
        {
            Sign, Unsupported
        }

        public string Args
        {
            get
            {
                var args = new List<string>();

                // 添加选项
                foreach (var opt in Options)
                {
                    var key = opt.Key;
                    var value = opt.Value;

                    // 特殊处理某些选项
                    switch (key)
                    {
                        case "[temp]":
                            key = "output";
                            break;
                        case "[chapter]":
                        case "[thumbnail]":
                        case "[subtitle]":
                            key = "output";
                            break;
                    }

                    // 确保合并选项正确传递
                    if (key == "format" && value.Contains("+"))
                    {
                        // 如果是格式选项且包含+号，确保merge-output-format选项存在
                        if (!Options.ContainsKey("merge-output-format"))
                        {
                            args.Add("--merge-output-format mp4");
                        }
                    }

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        args.Add($"--{key}");
                    }
                    else
                    {
                        // 对于包含空格的值，添加引号
                        if (value.Contains(" ") && !value.StartsWith("\"") && !value.EndsWith("\""))
                        {
                            args.Add($"--{key} \"{value}\"");
                        }
                        else
                        {
                            args.Add($"--{key} {value}");
                        }
                    }
                }

                // 添加URL
                if (!string.IsNullOrWhiteSpace(Url))
                {
                    // 对于包含空格的URL，添加引号
                    if (Url.Contains(" ") && !Url.StartsWith("\"") && !Url.EndsWith("\""))
                    {
                        args.Add($"\"{Url}\"");
                    }
                    else
                    {
                        args.Add(Url);
                    }
                }

                return string.Join(" ", args);
            }
        }

        private static Regex ErrSign = new Regex(@"^(?=.*?ERROR)(?=.*?sign)(?=.*?confirm)", RegexOptions.IgnoreCase);
        private static Regex ErrUnsupported = new Regex(@"^(?=.*?ERROR)(?=.*?Unsupported)", RegexOptions.IgnoreCase);

        public Process? Exec(Action<string>? stdall = null, Action<string>? stdout = null, Action<string>? stderr = null)
        {
            // 获取应用程序所在目录的绝对路径
            string appDir = AppContext.BaseDirectory;
            Console.WriteLine($"AppContext.BaseDirectory in DLP.Exec: {appDir}");

            var fn = Path_DLP;
            if (string.IsNullOrEmpty(fn) || !File.Exists(fn))
            {
                Console.WriteLine($"yt-dlp executable not found at: {fn}");

                // 首先检查deps目录中是否有yt-dlp
                string depsDir = Path.Combine(appDir, "deps");
                string ytdlpInDeps = Path.Combine(depsDir, "yt-dlp");

                if (File.Exists(ytdlpInDeps))
                {
                    fn = ytdlpInDeps;
                    Path_DLP = ytdlpInDeps; // 更新静态路径
                    Console.WriteLine($"Found yt-dlp in deps directory: {fn}");
                }
                else
                {
                    // 尝试查找系统yt-dlp
                    try
                    {
                        var whichPsi = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/which",
                            Arguments = "yt-dlp",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = appDir
                        };

                        using var whichProc = Process.Start(whichPsi);
                        string output = whichProc.StandardOutput.ReadToEnd().Trim();
                        whichProc.WaitForExit();

                        if (!string.IsNullOrWhiteSpace(output) && File.Exists(output))
                        {
                            fn = output;
                            Path_DLP = output; // 更新静态路径
                            Console.WriteLine($"Found system yt-dlp at: {fn}");
                        }
                        else
                        {
                            Console.WriteLine("yt-dlp executable not found in system path");
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error finding yt-dlp: {ex.Message}");
                        return null;
                    }
                }
            }

            // 设置工作目录为应用程序目录
            string workDir = appDir;

            // 确保工作目录存在
            if (string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir))
            {
                // 使用用户主目录作为备用工作目录
                workDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                Console.WriteLine($"Using user home directory as fallback working directory: {workDir}");
            }

            var info = new ProcessStartInfo()
            {
                FileName = fn,
                Arguments = Args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workDir
            };

            Debug.WriteLine($"{info.FileName} {info.Arguments}");

            process.StartInfo = info;
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += (s, e) =>
            {
                Debug.WriteLine(e.Data, "STD");
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    stdall?.Invoke(e.Data);
                    stdout?.Invoke(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                Debug.WriteLine(e.Data, "ERR");
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    stdall?.Invoke(e.Data);
                    stderr?.Invoke(e.Data);

                    if (ErrSign.IsMatch(e.Data))
                    {
                        StdErr.Add(DLPError.Sign);
                    }

                    if (ErrUnsupported.IsMatch(e.Data))
                    {
                        StdErr.Add(DLPError.Unsupported);
                    }
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit();

            return process;
        }

        public void Close()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // 忽略错误
            }
        }

        // 添加Cookie支持
        public DLP Cookie(CookieType type, bool enable = true)
        {
            if (enable)
            {
                switch (type)
                {
                    case CookieType.Chrome:
                        Options["cookies-from-browser"] = "chrome";
                        break;
                    case CookieType.Edge:
                        Options["cookies-from-browser"] = "edge";
                        break;
                    case CookieType.Firefox:
                        Options["cookies-from-browser"] = "firefox";
                        break;
                    case CookieType.Opera:
                        Options["cookies-from-browser"] = "opera";
                        break;
                    case CookieType.Safari:
                        Options["cookies-from-browser"] = "safari";
                        break;
                    case CookieType.Chromium:
                        Options["cookies-from-browser"] = "chromium";
                        break;
                    case CookieType.Chrome_Beta:
                        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        var cookiePath = Path.Combine(homeDir, "Library", "Application Support", "Google", "Chrome Beta");
                        Options["cookies-from-browser"] = $"chrome:{cookiePath}";
                        break;
                }
            }
            return this;
        }

        // 添加错误处理方法
        public DLP Err(DLPError err, Action callback)
        {
            if (StdErr.Contains(err))
            {
                callback.Invoke();
            }
            return this;
        }
    }

    public static class DLPExtend
    {
        public static string QS(this string str)
        {
            return $"\"{str}\"";
        }

        public static string QP(this string path, string prefix = "")
        {
            var p = path.Replace(Path.DirectorySeparatorChar, '/');
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return p.QS();
            }
            else
            {
                return $"{prefix}:{p}".QS();
            }
        }

        public static string RemoveExt(this string path)
        {
            if (path.isVideo() ||
                path.isAudio() ||
                path.isImage() ||
                path.isSubtitle())
            {
                return Path.ChangeExtension(path, null);
            }
            else
            {
                return path;
            }
        }

        public static bool isVideo(this string path)
        {
            if (!Path.HasExtension(path)) return false;
            var exts = new[] { "avi", "flv", "mkv", "mov", "mp4", "webm" };
            return exts.Contains(Path.GetExtension(path).ToLower().Trim('.'));
        }

        public static bool isAudio(this string path)
        {
            if (!Path.HasExtension(path)) return false;
            var exts = new[] { "aac", "flac", "m4a", "mp3", "ogg", "opus", "wav" };
            return exts.Contains(Path.GetExtension(path).ToLower().Trim('.'));
        }

        public static bool isImage(this string path)
        {
            if (!Path.HasExtension(path)) return false;
            var exts = new[] { "jpg", "jpeg", "png", "gif", "bmp", "webp" };
            return exts.Contains(Path.GetExtension(path).ToLower().Trim('.'));
        }

        public static bool isSubtitle(this string path)
        {
            if (!Path.HasExtension(path)) return false;
            var exts = new[] { "srt", "vtt", "ass" };
            return exts.Contains(Path.GetExtension(path).ToLower().Trim('.'));
        }
    }
}
