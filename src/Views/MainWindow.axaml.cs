using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using yt_dlp_gui_mac.Models;
using yt_dlp_gui_mac.Wrappers;
using Avalonia.Media.Imaging;
using System.Diagnostics;
using Avalonia.Platform.Storage;
using System.Security.Authentication;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Text;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System.Reactive;
using System.Reactive.Linq;

namespace yt_dlp_gui_mac.Views
{
    public partial class MainWindow : Window
    {
        private readonly ViewData Data = new();
        private List<DLP> RunningDLP = new();
        private bool _clipboardMonitoring = false;
        private System.Timers.Timer _clipboardTimer;
        private string _lastClipboardText = "";

        // 用于存储预加载的yt-dlp路径
        private string _preloadedYtdlpPath = string.Empty;
        private bool _ytdlpPreloaded = false;

        public MainWindow()
        {
            Console.WriteLine("MainWindow constructor called");

            try
            {
                InitializeComponent();
                Console.WriteLine("InitializeComponent completed");

                // Set data context
                DataContext = Data;
                Console.WriteLine("DataContext set");

                // Load configuration
                InitGUIConfig();
                Console.WriteLine("InitGUIConfig completed");

                // Set window properties
                if (Data.AlwaysOnTop)
                {
                    Topmost = true;
                }

                if (Data.RememberWindowStatePosition)
                {
                    Position = new Avalonia.PixelPoint((int)Data.Left, (int)Data.Top);
                }

                if (Data.RememberWindowStateSize)
                {
                    Width = Data.Width;
                    Height = Data.Height;
                }
                else
                {
                    Width = 600 * (Data.Scale / 100d);
                    Height = 380 * (Data.Scale / 100d);
                }

                Console.WriteLine("Window properties set");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MainWindow constructor: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            // Check configuration
            InitConfiguration();

            // Scan dependencies
            ScanDepends();

            // 预加载yt-dlp
            Task.Run(PreloadYtdlp);

            // Set target path
            if (!Directory.Exists(Data.TargetPath))
            {
                // Use user's Downloads folder as default download directory
                string userDownloadsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");
                
                Data.TargetPath = Directory.Exists(userDownloadsFolder) 
                    ? userDownloadsFolder 
                    : App.AppPath;
                
                Console.WriteLine($"Default download directory set to: {Data.TargetPath}");
            }

            // Set temporary directory
            if (string.IsNullOrWhiteSpace(Data.PathTEMP) || !Directory.Exists(GetTempPath))
            {
                Data.PathTEMP = "%YTDLPGUI_TARGET%";
            }

            // Initialize clipboard monitoring
            InitClipboard();

            // Run update check
            Task.Run(Inits);

            // Bind event handlers
            var analyzeButton = this.FindControl<Button>("AnalyzeButton");
            if (analyzeButton != null)
            {
                analyzeButton.Click += async (s, e) => await AnalyzeUrlAsync();
            }

            var downloadButton = this.FindControl<Button>("DownloadButton");
            if (downloadButton != null)
            {
                downloadButton.Click += Button_Download;
            }

            var cancelButton = this.FindControl<Button>("CancelButton");
            if (cancelButton != null)
            {
                cancelButton.Click += Button_Cancel;
            }

            var browseButton = this.FindControl<Button>("BrowseButton");
            if (browseButton != null)
            {
                browseButton.Click += Button_Browse;
            }
            
            var bottomBrowseButton = this.FindControl<Button>("BottomBrowseButton");
            if (bottomBrowseButton != null)
            {
                bottomBrowseButton.Click += Button_Browse;
            }

            // Get format dropdown
            var formatComboBox = this.FindControl<ComboBox>("FormatComboBox");

            // Listen for data changes
            Data.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Data.IsAnalyzed) && Data.IsAnalyzed)
                {
                    // After analysis is complete, update format dropdown
                    if (formatComboBox != null)
                    {
                        // Clear and add new formats
                        formatComboBox.Items.Clear();
                        foreach (var format in Data.FormatsVideo)
                        {
                            formatComboBox.Items.Add(format);
                        }
                        formatComboBox.SelectedItem = Data.selectedFormat;
                    }
                }
            };

            // Bind format dropdown events
            var videoCombo = this.FindControl<ComboBox>("VideoFormatComboBox");
            var audioCombo = this.FindControl<ComboBox>("AudioFormatComboBox");
            var subtitleCombo = this.FindControl<ComboBox>("SubtitleComboBox");
            var videoInfo = this.FindControl<TextBlock>("VideoInfoText");
            var audioInfo = this.FindControl<TextBlock>("AudioInfoText");
            var subtitleInfo = this.FindControl<TextBlock>("SubtitleInfoText");
            
            // Bind individual download buttons
            var videoDownloadBtn = this.FindControl<Button>("VideoDownloadButton");
            var audioDownloadBtn = this.FindControl<Button>("AudioDownloadButton");
            var subtitleDownloadBtn = this.FindControl<Button>("SubtitleDownloadButton");
            
            if (videoDownloadBtn != null)
                videoDownloadBtn.Click += (s, e) => Download_Format(videoCombo?.SelectedItem as Format);
                
            if (audioDownloadBtn != null)
                audioDownloadBtn.Click += (s, e) => Download_Format(audioCombo?.SelectedItem as Format);
                
            if (subtitleDownloadBtn != null)
                subtitleDownloadBtn.Click += (s, e) => Download_Format(subtitleCombo?.SelectedItem as Format);
            
            if (videoCombo != null)
                videoCombo.SelectionChanged += (s, e) =>
                {
                    if (videoCombo.SelectedItem is Format fmt)
                        videoInfo.Text = FormatToString(fmt);
                };
            if (audioCombo != null)
                audioCombo.SelectionChanged += (s, e) =>
                {
                    if (audioCombo.SelectedItem is Format fmt)
                        audioInfo.Text = FormatToString(fmt);
                };
            if (subtitleCombo != null)
                subtitleCombo.SelectionChanged += (s, e) =>
                {
                    if (subtitleCombo.SelectedItem is Format fmt)
                        subtitleInfo.Text = FormatToString(fmt);
                };

            // Bind clipboard monitoring button
            var monitorBtn = this.FindControl<Button>("MonitorClipboardButton");
            if (monitorBtn != null)
            {
                monitorBtn.Click += (s, e) => ToggleClipboardMonitoring();
            }

            // Get filename template input box
            var templateBox = this.FindControl<TextBox>("FilenameTemplateTextBox");
            if (templateBox != null)
            {
                // Set initial value
                templateBox.Text = Data.FilenameTemplate ?? "%(title)s.%(ext)s";
                
                // Bind text change event
                templateBox.TextChanged += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(templateBox.Text))
                    {
                        Data.FilenameTemplate = templateBox.Text;
                    }
                };
            }
            
            // Bind variables dropdown and insert button
            var variablesCombo = this.FindControl<ComboBox>("VariablesComboBox");
            var insertVariableBtn = this.FindControl<Button>("InsertVariableButton");

            if (variablesCombo != null && insertVariableBtn != null && templateBox != null)
            {
                // Handle insert button click
                insertVariableBtn.Click += (s, e) =>
                {
                    if (variablesCombo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
                    {
                        string variable = selectedItem.Tag.ToString();
                        int caretIndex = templateBox.CaretIndex;
                        string currentText = templateBox.Text ?? "";

                        // Insert the variable at caret position
                        string newText = currentText.Insert(caretIndex, variable);
                        templateBox.Text = newText;

                        // Move caret after the inserted variable
                        templateBox.CaretIndex = caretIndex + variable.Length;

                        // Set focus back to template box
                        templateBox.Focus();
                    }
                };

                // Also handle double-click on combo box item
                variablesCombo.SelectionChanged += (s, e) =>
                {
                    if (variablesCombo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
                    {
                        // Auto-insert on selection change is optional
                        // Uncomment the following code if you want auto-insert
                        /*
                        string variable = selectedItem.Tag.ToString();
                        int caretIndex = templateBox.CaretIndex;
                        string currentText = templateBox.Text ?? "";

                        // Insert the variable at caret position
                        string newText = currentText.Insert(caretIndex, variable);
                        templateBox.Text = newText;

                        // Move caret after the inserted variable
                        templateBox.CaretIndex = caretIndex + variable.Length;
                        */
                    }
                };
            }

            // Get "open folder after download" checkbox
            var openFolderCheckBox = this.FindControl<CheckBox>("OpenFolderAfterDownloadCheckBox");
            if (openFolderCheckBox != null)
            {
                // Set initial value
                openFolderCheckBox.IsChecked = Data.OpenFolderAfterDownload;
                
                // Bind checked state change event
                openFolderCheckBox.IsCheckedChanged += (s, e) =>
                {
                    Data.OpenFolderAfterDownload = openFolderCheckBox.IsChecked ?? true;
                };
            }

            // Bind advanced options
            var extraArgsBox = this.FindControl<TextBox>("ExtraArgsTextBox");
            var proxyBox = this.FindControl<TextBox>("ProxyTextBox");
            var subtitleLangBox = this.FindControl<TextBox>("SubtitleLangTextBox");
            var testButton = this.FindControl<Button>("TestButton");
            var resetButton = this.FindControl<Button>("ResetButton");
            
            if (extraArgsBox != null)
            {
                extraArgsBox.Text = Data.ExtraArgs;
                extraArgsBox.TextChanged += (s, e) => Data.ExtraArgs = extraArgsBox.Text;
            }
            
            if (proxyBox != null)
            {
                proxyBox.Text = Data.Proxy;
                proxyBox.TextChanged += (s, e) => Data.Proxy = proxyBox.Text;
            }
            
            if (subtitleLangBox != null)
            {
                // Set directly through binding, no additional code needed
            }
            
            if (testButton != null)
            {
                testButton.Click += async (s, e) => await TestDownloadAsync();
            }
            
            if (resetButton != null)
            {
                resetButton.Click += (s, e) => ResetAdvancedOptions();
            }

            // Bind thumbnail download checkbox
            var thumbCheckBox = this.FindControl<CheckBox>("DownloadThumbnailCheckBox");
            var playPauseBtn = this.FindControl<Button>("PlayPauseButton");
            var muteBtn = this.FindControl<Button>("MuteButton");
            var mediaPositionSlider = this.FindControl<Slider>("MediaPositionSlider");
            var mediaDurationText = this.FindControl<TextBlock>("MediaDurationText");

            if (thumbCheckBox != null)
            {
                thumbCheckBox.IsCheckedChanged += (s, e) => 
                {
                    Data.DownloadThumbnail = thumbCheckBox.IsChecked ?? false;
                };
            }

            if (playPauseBtn != null)
            {
                playPauseBtn.Click += (s, e) => ToggleMediaPlayback();
            }

            if (muteBtn != null)
            {
                muteBtn.Click += (s, e) => ToggleMute();
            }

            if (mediaPositionSlider != null)
            {
                mediaPositionSlider.ValueChanged += (s, e) => SeekMedia(e.NewValue);
            }

            // Bind open folder button
            var openFolderBtn = this.FindControl<Button>("OpenFolderButton");
            if (openFolderBtn != null)
            {
                openFolderBtn.Click += Button_OpenFolder;
            }

            // Bind Cookie settings
            var cookieUseCombo = this.FindControl<ComboBox>("CookieUseComboBox");
            var cookieTypeCombo = this.FindControl<ComboBox>("CookieTypeComboBox");

            if (cookieUseCombo != null)
            {
                // Set initial value
                switch (Data.UseCookie)
                {
                    case UseCookie.WhenNeeded:
                        cookieUseCombo.SelectedIndex = 0;
                        break;
                    case UseCookie.Always:
                        cookieUseCombo.SelectedIndex = 1;
                        break;
                    case UseCookie.Ask:
                        cookieUseCombo.SelectedIndex = 2;
                        break;
                    case UseCookie.Never:
                        cookieUseCombo.SelectedIndex = 3;
                        break;
                }

                // Bind selection change event
                cookieUseCombo.SelectionChanged += (s, e) =>
                {
                    if (cookieUseCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
                    {
                        string tag = item.Tag.ToString();
                        switch (tag)
                        {
                            case "WhenNeeded":
                                Data.UseCookie = UseCookie.WhenNeeded;
                                break;
                            case "Always":
                                Data.UseCookie = UseCookie.Always;
                                break;
                            case "Ask":
                                Data.UseCookie = UseCookie.Ask;
                                break;
                            case "Never":
                                Data.UseCookie = UseCookie.Never;
                                break;
                        }
                        Console.WriteLine($"Cookie usage policy set to: {Data.UseCookie}");
                    }
                };
            }

            if (cookieTypeCombo != null)
            {
                // Set initial value
                switch (Data.CookieType)
                {
                    case CookieType.Chrome:
                        cookieTypeCombo.SelectedIndex = 0;
                        break;
                    case CookieType.Safari:
                        cookieTypeCombo.SelectedIndex = 1;
                        break;
                    case CookieType.Firefox:
                        cookieTypeCombo.SelectedIndex = 2;
                        break;
                    case CookieType.Edge:
                        cookieTypeCombo.SelectedIndex = 3;
                        break;
                    case CookieType.Opera:
                        cookieTypeCombo.SelectedIndex = 4;
                        break;
                    case CookieType.Chromium:
                        cookieTypeCombo.SelectedIndex = 5;
                        break;
                    case CookieType.Chrome_Beta:
                        cookieTypeCombo.SelectedIndex = 6;
                        break;
                }

                // Bind selection change event
                cookieTypeCombo.SelectionChanged += (s, e) =>
                {
                    if (cookieTypeCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
                    {
                        string tag = item.Tag.ToString();
                        switch (tag)
                        {
                            case "Chrome":
                                Data.CookieType = CookieType.Chrome;
                                break;
                            case "Safari":
                                Data.CookieType = CookieType.Safari;
                                break;
                            case "Firefox":
                                Data.CookieType = CookieType.Firefox;
                                break;
                            case "Edge":
                                Data.CookieType = CookieType.Edge;
                                break;
                            case "Opera":
                                Data.CookieType = CookieType.Opera;
                                break;
                            case "Chromium":
                                Data.CookieType = CookieType.Chromium;
                                break;
                            case "Chrome_Beta":
                                Data.CookieType = CookieType.Chrome_Beta;
                                break;
                        }
                        Console.WriteLine($"Cookie type set to: {Data.CookieType}");
                    }
                };
            }

            // Bind auto-subtitle checkbox
            var autoSubCheckBox = this.FindControl<CheckBox>("AutoSubtitleCheckBox");
            if (autoSubCheckBox != null)
            {
                autoSubCheckBox.IsCheckedChanged += (s, e) => 
                {
                    Data.IncludeAutoSubtitles = autoSubCheckBox.IsChecked ?? true;
                    // If video has already been analyzed, fetch subtitles again
                    if (Data.IsAnalyzed && !string.IsNullOrEmpty(Data.Url))
                    {
                        var subtitleCombo = this.FindControl<ComboBox>("SubtitleComboBox");
                        var subtitleInfo = this.FindControl<TextBlock>("SubtitleInfoText");
                        Task.Run(() => FetchSubtitlesAsync(Data.Url, subtitleCombo, subtitleInfo));
                    }
                };
            }
        }

        private string GetTempPath => GetEnvPath(Data.PathTEMP);

        private string GetEnvPath(string path)
        {
            Dictionary<string, string> replacements = new()
            {
                {"%YTDLPGUI_TARGET%", Data.TargetPath},
                {"%YTDLPGUI_LOCALE%", App.AppPath}
            };

            foreach (KeyValuePair<string, string> pair in replacements)
            {
                string placeholder = pair.Key;
                string replacement = pair.Value;

                // Replace the placeholder with the replacement string
                path = path.Replace(placeholder, replacement);

                // Remove the part to the left of the replacement string
                int index = path.IndexOf(replacement);
                if (index >= 0)
                {
                    path = path.Substring(index);
                }

                // Remove duplicate directory separators
                path = path.Replace('/', Path.DirectorySeparatorChar);
                path = path.Replace('\\', Path.DirectorySeparatorChar);
                while (path.Contains(string.Concat(Path.DirectorySeparatorChar, Path.DirectorySeparatorChar)))
                {
                    path = path.Replace(string.Concat(Path.DirectorySeparatorChar, Path.DirectorySeparatorChar),
                                       Path.DirectorySeparatorChar.ToString());
                }
            }

            return Environment.ExpandEnvironmentVariables(path);
        }

        public void InitGUIConfig()
        {
            // Load GUI configuration
            Data.GUIConfig.Load(App.Path(App.Folders.root, App.AppName + ".yaml"));
            Libs.Util.PropertyCopy(Data.GUIConfig, Data);
            Data.AutoSaveConfig = true;
        }

        private void InitConfiguration()
        {
            Console.WriteLine("Initializing configuration...");
            
            // Set default download directory to user's Downloads folder
            string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(downloadPath))
            {
                // If Downloads folder doesn't exist, try to create it
                try
                {
                    Directory.CreateDirectory(downloadPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create Downloads folder: {ex.Message}");
                    // Fall back to application directory
                    downloadPath = AppDomain.CurrentDomain.BaseDirectory;
                }
            }
            
            // Set default download directory
            Data.TargetPath = downloadPath;
            Console.WriteLine($"Default download directory set to: {downloadPath}");
            
            // Set filename template
            Data.FilenameTemplate = "%(title)s.%(ext)s";
            
            // Set other default options
            Data.SubLang = "en.*,ja.*,zh.*";
            Data.IncludeAutoSubtitles = true;
            Data.OpenFolderAfterDownload = true;
            
            Console.WriteLine("Configuration initialization complete");
        }

        private void ScanDepends()
        {
            // Scan dependencies
            var isYoutubeDl = @"^youtube-dl$";

            Console.WriteLine("Scanning for dependencies...");

            // 首先检查用户配置的路径
            if (!string.IsNullOrWhiteSpace(Data.PathYTDLP) && File.Exists(Data.PathYTDLP))
            {
                DLP.Path_DLP = Data.PathYTDLP;
                Console.WriteLine($"Using configured yt-dlp path: {DLP.Path_DLP}");
            }

            if (!string.IsNullOrWhiteSpace(Data.PathAria2) && File.Exists(Data.PathAria2))
            {
                DLP.Path_Aria2 = Data.PathAria2;
                Console.WriteLine($"Using configured aria2 path: {DLP.Path_Aria2}");
            }

            if (!string.IsNullOrWhiteSpace(Data.PathFFMPEG) && File.Exists(Data.PathFFMPEG))
            {
                DLP.Path_FFMPEG = Data.PathFFMPEG;
                FFMPEG.Path_FFMPEG = Data.PathFFMPEG;
                Console.WriteLine($"Using configured ffmpeg path: {DLP.Path_FFMPEG}");
            }

            // 检查是否需要搜索依赖项
            bool needScan = string.IsNullOrWhiteSpace(DLP.Path_DLP) ||
                            string.IsNullOrWhiteSpace(DLP.Path_Aria2) ||
                            string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG);

            if (needScan)
            {
                Console.WriteLine("Searching for dependencies in application directory...");

                // 首先检查 macOS 应用包中的 deps 目录
                string depsDir = Path.Combine(App.AppPath, "deps");
                if (Directory.Exists(depsDir))
                {
                    Console.WriteLine($"Found deps directory: {depsDir}");

                    // 检查 deps 目录中的文件
                    var depsFiles = Directory.EnumerateFiles(depsDir).ToList();

                    foreach (var file in depsFiles)
                    {
                        Console.WriteLine($"Found dependency file: {file}");
                    }

                    // 查找 yt-dlp
                    var depYtdlp = depsFiles.FirstOrDefault(x => Path.GetFileName(x).Equals("yt-dlp", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(depYtdlp))
                    {
                        DLP.Path_DLP = depYtdlp;
                        Data.PathYTDLP = depYtdlp;
                        Console.WriteLine($"Found yt-dlp in deps directory: {DLP.Path_DLP}");
                    }

                    // 查找 ffmpeg
                    var depFfmpeg = depsFiles.FirstOrDefault(x => Path.GetFileName(x).Equals("ffmpeg", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(depFfmpeg))
                    {
                        FFMPEG.Path_FFMPEG = depFfmpeg;
                        Data.PathFFMPEG = depFfmpeg;
                        Console.WriteLine($"Found ffmpeg in deps directory: {FFMPEG.Path_FFMPEG}");
                    }

                    // 查找 aria2c
                    var depAria2 = depsFiles.FirstOrDefault(x =>
                        Path.GetFileName(x).Equals("aria2c", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(x).Equals("aria2", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(depAria2))
                    {
                        DLP.Path_Aria2 = depAria2;
                        Data.PathAria2 = depAria2;
                        Console.WriteLine($"Found aria2 in deps directory: {DLP.Path_Aria2}");
                    }
                }

                // 如果在 deps 目录中没有找到，则在整个应用目录中搜索
            if (string.IsNullOrWhiteSpace(DLP.Path_DLP) ||
                string.IsNullOrWhiteSpace(DLP.Path_Aria2) ||
                string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG))
            {
                    Console.WriteLine("Searching in entire application directory...");

                var deps = Directory.EnumerateFiles(App.AppPath, "*", SearchOption.AllDirectories)
                    .Where(x => !x.EndsWith(".dll") && !x.EndsWith(".pdb") && !x.EndsWith(".config"))
                    .ToList();

                var dep_ytdlp = deps.FirstOrDefault(x => Regex.IsMatch(Path.GetFileName(x), @"^(yt-dlp(_min|_x86|_x64)?|ytdl-patched.*?)$"), "");
                var dep_ffmpeg = deps.FirstOrDefault(x => Regex.IsMatch(Path.GetFileName(x), @"^ffmpeg"), "");
                var dep_aria2 = deps.FirstOrDefault(x => Regex.IsMatch(Path.GetFileName(x), @"^aria2"), "");
                var dep_youtubedl = deps.FirstOrDefault(x => Regex.IsMatch(Path.GetFileName(x), isYoutubeDl), "");

                    // Set dependency paths
                    if (!string.IsNullOrWhiteSpace(dep_ytdlp) && string.IsNullOrWhiteSpace(DLP.Path_DLP))
                {
                    DLP.Path_DLP = dep_ytdlp;
                    Data.PathYTDLP = dep_ytdlp;
                        Console.WriteLine($"Found yt-dlp in application directory: {DLP.Path_DLP}");
                }
                    else if (!string.IsNullOrWhiteSpace(dep_youtubedl) && string.IsNullOrWhiteSpace(DLP.Path_DLP))
                {
                    DLP.Path_DLP = dep_youtubedl;
                    Data.PathYTDLP = dep_youtubedl;
                    DLP.Type = DLP.DLPType.youtube_dl;
                        Console.WriteLine($"Found youtube-dl in application directory: {DLP.Path_DLP}");
                }

                    if (!string.IsNullOrWhiteSpace(dep_ffmpeg) && string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG))
                {
                    FFMPEG.Path_FFMPEG = dep_ffmpeg;
                    Data.PathFFMPEG = dep_ffmpeg;
                        Console.WriteLine($"Found ffmpeg in application directory: {FFMPEG.Path_FFMPEG}");
                }

                    if (!string.IsNullOrWhiteSpace(dep_aria2) && string.IsNullOrWhiteSpace(DLP.Path_Aria2))
                {
                    DLP.Path_Aria2 = dep_aria2;
                    Data.PathAria2 = dep_aria2;
                        Console.WriteLine($"Found aria2 in application directory: {DLP.Path_Aria2}");
                    }
                }

                // 检查 macOS 应用包中的特定路径
                try
                {
                    // 尝试多个可能的路径
                    var possiblePaths = new List<string>
                    {
                        Path.Combine(App.AppPath, "..", "MacOS", "deps"),
                        Path.Combine(App.AppPath, "Contents", "MacOS", "deps"),
                        Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "deps"),
                        Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "..", "deps"),
                        Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "..", "MacOS", "deps"),
                        Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "..", "..", "deps"),
                        "/Applications/yt-dlp-gui.app/Contents/MacOS/deps"
                    };

                    foreach (var path in possiblePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            Console.WriteLine($"Found deps directory: {path}");

                            // 检查 deps 目录中的文件
                            var bundleDepsFiles = Directory.EnumerateFiles(path).ToList();

                            foreach (var file in bundleDepsFiles)
                            {
                                Console.WriteLine($"Found dependency file: {file}");
                            }

                            // 查找 yt-dlp
                            var bundleYtdlp = bundleDepsFiles.FirstOrDefault(x => Path.GetFileName(x).Equals("yt-dlp", StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrWhiteSpace(bundleYtdlp) && string.IsNullOrWhiteSpace(DLP.Path_DLP))
                            {
                                DLP.Path_DLP = bundleYtdlp;
                                Data.PathYTDLP = bundleYtdlp;
                                Console.WriteLine($"Found yt-dlp in deps directory: {DLP.Path_DLP}");
                            }

                            // 查找 ffmpeg
                            var bundleFfmpeg = bundleDepsFiles.FirstOrDefault(x => Path.GetFileName(x).Equals("ffmpeg", StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrWhiteSpace(bundleFfmpeg) && string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG))
                            {
                                FFMPEG.Path_FFMPEG = bundleFfmpeg;
                                Data.PathFFMPEG = bundleFfmpeg;
                                Console.WriteLine($"Found ffmpeg in deps directory: {FFMPEG.Path_FFMPEG}");
                            }

                            // 查找 aria2c
                            var bundleAria2 = bundleDepsFiles.FirstOrDefault(x =>
                                Path.GetFileName(x).Equals("aria2c", StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileName(x).Equals("aria2", StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrWhiteSpace(bundleAria2) && string.IsNullOrWhiteSpace(DLP.Path_Aria2))
                            {
                                DLP.Path_Aria2 = bundleAria2;
                                Data.PathAria2 = bundleAria2;
                                Console.WriteLine($"Found aria2 in deps directory: {DLP.Path_Aria2}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error searching for dependencies: {ex.Message}");
                }
            }

            // 如果仍然找不到ffmpeg，尝试使用系统ffmpeg
            if (string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG))
            {
                string systemFfmpeg = "/usr/local/bin/ffmpeg";
                if (File.Exists(systemFfmpeg))
                {
                    FFMPEG.Path_FFMPEG = systemFfmpeg;
                    Data.PathFFMPEG = systemFfmpeg;
                    Console.WriteLine($"Using system ffmpeg at: {systemFfmpeg}");
                }
                else
                {
                    // 尝试使用which命令查找ffmpeg
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/which",
                            Arguments = "ffmpeg",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var proc = Process.Start(psi);
                        string output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit();

                        if (!string.IsNullOrWhiteSpace(output) && File.Exists(output))
                        {
                            FFMPEG.Path_FFMPEG = output;
                            Data.PathFFMPEG = output;
                            Console.WriteLine($"Found system ffmpeg at: {output}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error finding ffmpeg: {ex.Message}");
                    }
                }
            }

            // 输出最终的依赖项路径
            Console.WriteLine($"Final yt-dlp path: {DLP.Path_DLP}");
            Console.WriteLine($"Final ffmpeg path: {FFMPEG.Path_FFMPEG}");
            Console.WriteLine($"Final aria2 path: {DLP.Path_Aria2}");
        }

        private void InitClipboard()
        {
            _clipboardTimer = new System.Timers.Timer(1000); // Check once per second
            _clipboardTimer.Elapsed += async (s, e) => await CheckClipboard();
            _clipboardTimer.AutoReset = true;
            
            if (Data.AutoMonitorClipboard)
            {
                _clipboardMonitoring = true;
                _clipboardTimer.Start();
                Console.WriteLine("Clipboard monitoring started");
            }
        }

        /// <summary>
        /// 预加载yt-dlp，避免首次分析时的延迟
        /// </summary>
        private async Task PreloadYtdlp()
        {
            Console.WriteLine("开始预加载yt-dlp...");

            try
            {
                // 获取应用程序所在目录的绝对路径
                string appDir = AppContext.BaseDirectory;
                Console.WriteLine($"应用目录: {appDir}");

                // 确定yt-dlp路径
                string ytdlpPath;
                string depsDir = Path.Combine(appDir, "deps");

                // 首先检查deps目录中是否有yt-dlp
                string ytdlpInDepsPath = Path.Combine(depsDir, "yt-dlp");
                if (File.Exists(ytdlpInDepsPath))
                {
                    ytdlpPath = ytdlpInDepsPath;
                    Console.WriteLine($"在deps目录中找到yt-dlp: {ytdlpPath}");
                }
                else if (!string.IsNullOrEmpty(DLP.Path_DLP) && File.Exists(DLP.Path_DLP))
                {
                    ytdlpPath = DLP.Path_DLP;
                    Console.WriteLine($"使用配置的yt-dlp路径: {ytdlpPath}");
                }
                else
                {
                    // 如果找不到，使用系统yt-dlp
                    ytdlpPath = "/usr/local/bin/yt-dlp";
                    if (!File.Exists(ytdlpPath))
                    {
                        // 尝试使用which命令查找yt-dlp
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
                            string output = await whichProc.StandardOutput.ReadToEndAsync();
                            await whichProc.WaitForExitAsync();

                            if (!string.IsNullOrWhiteSpace(output) && File.Exists(output.Trim()))
                            {
                                ytdlpPath = output.Trim();
                                Console.WriteLine($"在系统中找到yt-dlp: {ytdlpPath}");
                            }
                            else
                            {
                                Console.WriteLine("未找到yt-dlp可执行文件");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"查找yt-dlp时出错: {ex.Message}");
                            return;
                        }
                    }
                }

                // 设置工作目录
                string workDir = appDir;

                // 确保工作目录存在
                if (string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir))
                {
                    // 如果应用目录不存在，使用用户主目录
                    workDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    Console.WriteLine($"使用用户主目录作为备用: {workDir}");
                }

                // 如果 yt-dlp 路径是相对路径，转换为绝对路径
                if (!string.IsNullOrEmpty(ytdlpPath) && !Path.IsPathRooted(ytdlpPath))
                {
                    // 尝试在应用目录的 deps 文件夹中查找
                    string depsPath = Path.Combine(workDir, "deps");
                    if (Directory.Exists(depsPath))
                    {
                        string ytdlpInDeps = Path.Combine(depsPath, Path.GetFileName(ytdlpPath));
                        if (File.Exists(ytdlpInDeps))
                        {
                            ytdlpPath = ytdlpInDeps;
                        }
                    }
                }

                // 确保yt-dlp可执行文件存在
                if (!File.Exists(ytdlpPath))
                {
                    Console.WriteLine("未找到yt-dlp可执行文件");
                    return;
                }

                // 测试yt-dlp是否可用
                var psi = new ProcessStartInfo
                {
                    FileName = ytdlpPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workDir
                };

                using var proc = Process.Start(psi);
                string versionOutput = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(versionOutput))
                {
                    Console.WriteLine($"yt-dlp预加载成功，版本: {versionOutput.Trim()}");
                    _preloadedYtdlpPath = ytdlpPath;
                    _ytdlpPreloaded = true;
                }
                else
                {
                    Console.WriteLine("yt-dlp预加载失败");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"预加载yt-dlp时出错: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void ToggleClipboardMonitoring()
        {
            _clipboardMonitoring = !_clipboardMonitoring;
            
            if (_clipboardMonitoring)
            {
                _clipboardTimer.Start();
                Console.WriteLine("Clipboard monitoring started");
                
                // Update button visual effect to active state
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var monitorBtn = this.FindControl<Button>("MonitorClipboardButton");
                    if (monitorBtn != null)
                    {
                        monitorBtn.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#B0FF57"));
                        monitorBtn.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333"));
                    }
                });
            }
            else
            {
                _clipboardTimer.Stop();
                Console.WriteLine("Clipboard monitoring stopped");
                
                // Update button visual effect to inactive state
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var monitorBtn = this.FindControl<Button>("MonitorClipboardButton");
                    if (monitorBtn != null)
                    {
                        monitorBtn.Background = null;
                        monitorBtn.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#B0FF57"));
                    }
                });
            }
        }

        private async Task CheckClipboard()
        {
            try
            {
                // Get clipboard content
                string clipText = await GetClipboardTextAsync();
                
                // If clipboard content is the same as last time, return directly
                if (string.IsNullOrEmpty(clipText) || clipText == _lastClipboardText)
                {
                    return;
                }
                
                // Update last clipboard content
                _lastClipboardText = clipText;
                
                // Check if it's a video URL
                if (IsVideoUrl(clipText))
                {
                    Console.WriteLine($"Video link detected: {clipText}");
                    
                    // Update URL textbox on UI thread
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var urlBox = this.FindControl<TextBox>("UrlTextBox");
                        if (urlBox != null && string.IsNullOrWhiteSpace(urlBox.Text))
                        {
                            urlBox.Text = clipText;
                            Data.Url = clipText;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking clipboard: {ex.Message}");
            }
        }

        private async Task<string> GetClipboardTextAsync()
        {
            try
            {
                // Get clipboard text on macOS
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    var text = await clipboard.GetTextAsync();
                    return text ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting clipboard text: {ex.Message}");
            }
            
            return string.Empty;
        }



        /// <summary>
        /// 将Format列表转换为VideoFormat列表
        /// </summary>
        private List<VideoFormat> ConvertToVideoFormats(List<Format> formats)
        {
            var result = new List<VideoFormat>();

            foreach (var format in formats)
            {
                var videoFormat = new VideoFormat
                {
                    format_id = format.format_id,
                    format_note = format.format_note,
                    ext = format.ext,
                    acodec = format.acodec,
                    vcodec = format.vcodec,
                    width = format.width,
                    height = format.height,
                    fps = format.fps,
                    abr = format.abr,
                    vbr = format.vbr,
                    tbr = format.tbr,
                    format = format.format
                };

                result.Add(videoFormat);
            }

            return result;
        }

        /// <summary>
        /// 异步加载缩略图
        /// </summary>
        private async Task LoadThumbnailAsync(string thumbUrl, Image thumbImg)
        {
            if (string.IsNullOrWhiteSpace(thumbUrl) || thumbImg == null)
            {
                return;
            }

            try
            {
                // 计算缓存键
                string cacheKey = ComputeHash(thumbUrl);
                string cachePath = Path.Combine(GetThumbnailCacheDirectory(), cacheKey + ".jpg");

                // 检查缓存
                if (File.Exists(cachePath))
                {
                    // 从缓存加载
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            using var fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read);
                            var bitmap = new Bitmap(fileStream);
                            thumbImg.Source = bitmap;
                            Console.WriteLine("从缓存加载缩略图成功");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"从缓存加载缩略图失败: {ex.Message}");
                        }
                    });
                    return;
                }

                // 创建自定义证书验证处理程序
                var handler = new System.Net.Http.HttpClientHandler
                {
                    // 完全忽略SSL证书验证
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true,

                    // 禁用证书吊销检查
                    CheckCertificateRevocationList = false,

                    // 支持所有TLS版本
                    SslProtocols = System.Security.Authentication.SslProtocols.Tls |
                                  System.Security.Authentication.SslProtocols.Tls11 |
                                  System.Security.Authentication.SslProtocols.Tls12 |
                                  System.Security.Authentication.SslProtocols.Tls13,

                    // 允许自动重定向
                    AllowAutoRedirect = true,

                    // 允许自动解压缩
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };

                // 使用配置的处理程序创建HttpClient
                using var client = new System.Net.Http.HttpClient(handler);

                // 设置请求头以模拟常见浏览器
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,zh-CN;q=0.8,zh;q=0.7");

                // 设置合理的超时时间
                client.Timeout = TimeSpan.FromSeconds(10);

                try
                {
                    // 下载缩略图
                    var response = await client.GetAsync(thumbUrl);
                    response.EnsureSuccessStatusCode();

                    // 保存到缓存
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(cachePath, FileMode.Create))
                    {
                        await stream.CopyToAsync(fileStream);
                    }

                    Console.WriteLine($"缩略图已下载并缓存: {cachePath}");

                    // 加载到UI
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            using var fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read);
                            var bitmap = new Bitmap(fileStream);
                            thumbImg.Source = bitmap;
                            Console.WriteLine("缩略图已加载到UI");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"加载缩略图到UI失败: {ex.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"下载缩略图失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"缩略图加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 计算字符串的MD5哈希值
        /// </summary>
        private string ComputeHash(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // 转换为十六进制字符串
                var sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// 获取缩略图缓存目录
        /// </summary>
        private string GetThumbnailCacheDirectory()
        {
            string cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "yt-dlp-gui", "thumbnails");

            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            return cacheDir;
        }

        /// <summary>
        /// 更新按钮状态（启用/禁用、文本）
        /// </summary>
        /// <param name="button">要更新的按钮</param>
        /// <param name="isProcessing">是否正在处理中</param>
        /// <param name="text">按钮文本</param>
        private void UpdateButtonState(Button? button, bool isProcessing, string text)
        {
            if (button == null) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // 更新按钮文本
                button.Content = text;

                // 更新按钮状态
                button.IsEnabled = !isProcessing;

                // 更新按钮样式
                if (isProcessing)
                {
                    // 处理中状态 - 灰色
                    button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#808080"));
                }
                else
                {
                    // 恢复原始颜色
                    if (button.Name == "AnalyzeButton")
                    {
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E88E5"));
                    }
                    else if (button.Name == "DownloadButton")
                    {
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50"));
                    }
                    else if (button.Name == "VideoDownloadButton")
                    {
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50"));
                    }
                    else if (button.Name == "AudioDownloadButton")
                    {
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2196F3"));
                    }
                    else if (button.Name == "SubtitleDownloadButton")
                    {
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FF9800"));
                    }
                    else
                    {
                        // 默认颜色
                        button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E88E5"));
                    }
                }
            });
        }

        private bool IsVideoUrl(string text)
        {
            // Check URLs from common video websites
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.StartsWith("http://") || text.StartsWith("https://")) &&
                   (text.Contains("youtube.com/watch") ||
                    text.Contains("youtu.be/") ||
                    text.Contains("bilibili.com/video") ||
                    text.Contains("vimeo.com/") ||
                    text.Contains("dailymotion.com/video") ||
                    text.Contains("twitch.tv/") ||
                    text.Contains("facebook.com/watch") ||
                    text.Contains("instagram.com/p/") ||
                    text.Contains("twitter.com/") ||
                    text.Contains("tiktok.com/"));
        }

        private async void Inits()
        {
            // Check for updates
        }

        private string FormatToString(Format fmt)
        {
            if (fmt == null) return "";
            return $"{fmt.format_id} | {fmt.ext} | {fmt.width ?? 0}x{fmt.height ?? 0} | {fmt.vcodec}/{fmt.acodec} | {fmt.abr ?? 0}kbps | {fmt.format_note}";
        }

        /// <summary>
        /// 处理视频格式信息并更新UI
        /// </summary>
        private async Task ProcessVideoFormatsAsync(Video video, ComboBox? videoCombo, ComboBox? audioCombo, TextBlock? videoInfo, TextBlock? audioInfo)
        {
            await Task.Run(() => {
                try
                {
                    // 获取格式列表并转换为Format列表
                    var formats = new List<Format>();
                    if (video.formats != null)
                    {
                        foreach (var vf in video.formats)
                        {
                            formats.Add(new Format
                            {
                                format_id = vf.format_id,
                                format_note = vf.format_note,
                                ext = vf.ext,
                                acodec = vf.acodec,
                                vcodec = vf.vcodec,
                                width = vf.width,
                                height = vf.height,
                                fps = vf.fps,
                                abr = vf.abr,
                                vbr = vf.vbr,
                                tbr = vf.tbr,
                                format = vf.format
                            });
                        }
                    }

                    // 筛选视频和音频格式
                    var videoList = formats.Where(f => f.vcodec != null && f.vcodec != "none").ToList();
                    var audioList = formats.Where(f => f.acodec != null && f.acodec != "none" && (f.vcodec == null || f.vcodec == "none")).ToList();

                    Console.WriteLine($"视频格式: {videoList.Count}个, 音频格式: {audioList.Count}个");

                    // 输出找到的视频格式详情
                    foreach (var vf in videoList.Take(5)) // 只输出前5个，避免日志过长
                    {
                        Console.WriteLine($"视频格式: {vf.format_id} - {vf.format_note} ({vf.width}x{vf.height}) [{vf.ext}] {vf.vcodec}/{vf.acodec}");
                    }

                    // 按质量排序
                    videoList.Sort((a, b) => {
                        // 首先比较分辨率
                        int resA = (a.width ?? 0) * (a.height ?? 0);
                        int resB = (b.width ?? 0) * (b.height ?? 0);

                        if (resA != resB)
                            return resB.CompareTo(resA); // 降序排列

                        // 然后比较帧率
                        double fpsA = a.fps ?? 0;
                        double fpsB = b.fps ?? 0;

                        if (fpsA != fpsB)
                            return fpsB.CompareTo(fpsA); // 降序排列

                        // 最后比较比特率
                        double bitrateA = a.tbr ?? 0;
                        double bitrateB = b.tbr ?? 0;

                        return bitrateB.CompareTo(bitrateA); // 降序排列
                    });

                    // 按比特率排序音频
                    audioList.Sort((a, b) => {
                        double bitrateA = a.abr ?? 0;
                        double bitrateB = b.abr ?? 0;

                        return bitrateB.CompareTo(bitrateA); // 降序排列
                    });

                    // 在UI线程上更新UI
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        // 填充下拉框
                        if (videoCombo != null)
                        {
                            videoCombo.ItemsSource = videoList;
                            videoCombo.SelectedIndex = videoList.Count > 0 ? 0 : -1;

                            if (videoInfo != null && videoList.Count > 0)
                            {
                                videoInfo.Text = FormatToString(videoList[0]);
                            }

                            // 预更新文件名（使用第一个视频格式）
                            if (videoList.Count > 0)
                            {
                                UpdateTargetFileName(Data.TargetPath, Data.FilenameTemplate, videoList[0].ext);
                            }
                        }

                        if (audioCombo != null)
                        {
                            audioCombo.ItemsSource = audioList;
                            audioCombo.SelectedIndex = audioList.Count > 0 ? 0 : -1;

                            if (audioInfo != null && audioList.Count > 0)
                            {
                                audioInfo.Text = FormatToString(audioList[0]);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理视频格式时出错: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            });
        }

        private async Task AnalyzeUrlAsync()
        {
            var urlBox = this.FindControl<TextBox>("UrlTextBox");
            var infoBox = this.FindControl<TextBox>("VideoInfoBox");
            var thumbImg = this.FindControl<Image>("ThumbnailImage");
            var videoCombo = this.FindControl<ComboBox>("VideoFormatComboBox");
            var audioCombo = this.FindControl<ComboBox>("AudioFormatComboBox");
            var subtitleCombo = this.FindControl<ComboBox>("SubtitleComboBox");
            var videoInfo = this.FindControl<TextBlock>("VideoInfoText");
            var audioInfo = this.FindControl<TextBlock>("AudioInfoText");
            var subtitleInfo = this.FindControl<TextBlock>("SubtitleInfoText");
            var analyzeButton = this.FindControl<Button>("AnalyzeButton");

            if (urlBox == null || string.IsNullOrWhiteSpace(urlBox.Text))
            {
                infoBox.Text = "Please enter a valid video URL";
                return;
            }

            // 更新UI状态 - 分析中
            infoBox.Text = "Analyzing...";
            UpdateButtonState(analyzeButton, true, "Analyzing...");

            string url = urlBox.Text.Trim();

            // 设置分析状态
            Data.IsAnalyzing = true;

            try
            {
                // Set URL to Data model
                Data.Url = url;
                
                // Check if it's a special domain
                bool isSpecialDomain = url.Contains("pornhub") || 
                                      url.Contains("xvideos") || 
                                      url.Contains("xnxx") ||
                                      url.Contains("xhamster") ||
                                      url.Contains("youjizz") ||
                                      url.Contains("youku.com");

                // Call yt-dlp --dump-json
                // 使用预加载的yt-dlp路径或重新查找
                string ytdlpPath;
                string workDir = AppContext.BaseDirectory;

                if (_ytdlpPreloaded && !string.IsNullOrEmpty(_preloadedYtdlpPath) && File.Exists(_preloadedYtdlpPath))
                {
                    // 使用预加载的yt-dlp路径
                    ytdlpPath = _preloadedYtdlpPath;
                    Console.WriteLine($"使用预加载的yt-dlp路径: {ytdlpPath}");
                }
                else
                {
                    // 如果预加载失败，重新查找yt-dlp
                    Console.WriteLine("预加载的yt-dlp不可用，重新查找...");

                    // 获取应用程序所在目录的绝对路径
                    string appDir = AppContext.BaseDirectory;
                    Console.WriteLine($"AppContext.BaseDirectory: {appDir}");

                    string depsDir = Path.Combine(appDir, "deps");

                    // 首先检查deps目录中是否有yt-dlp
                    string ytdlpInDepsPath = Path.Combine(depsDir, "yt-dlp");
                    if (File.Exists(ytdlpInDepsPath))
                    {
                        ytdlpPath = ytdlpInDepsPath;
                        Console.WriteLine($"Found yt-dlp in deps directory: {ytdlpPath}");
                    }
                    else if (!string.IsNullOrEmpty(DLP.Path_DLP) && File.Exists(DLP.Path_DLP))
                    {
                        ytdlpPath = DLP.Path_DLP;
                        Console.WriteLine($"Using configured yt-dlp path: {ytdlpPath}");
                    }
                    else
                    {
                        // 如果找不到，使用系统yt-dlp
                        ytdlpPath = "/usr/local/bin/yt-dlp";
                        if (!File.Exists(ytdlpPath))
                        {
                            // 尝试使用which命令查找yt-dlp
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
                                    ytdlpPath = output;
                                    Console.WriteLine($"Found system yt-dlp at: {ytdlpPath}");
                                }
                                else
                                {
                                    infoBox.Text = "Error: yt-dlp executable not found. Please install yt-dlp or specify the correct path in settings.";
                                    Console.WriteLine("yt-dlp executable not found");
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                infoBox.Text = $"Error finding yt-dlp: {ex.Message}";
                                Console.WriteLine($"Error finding yt-dlp: {ex.Message}");
                                return;
                            }
                        }
                    }

                    // 设置工作目录
                    workDir = appDir;

                    // 确保工作目录存在
                    if (string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir))
                    {
                        // 如果应用目录不存在，使用用户主目录
                        workDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        Console.WriteLine($"Using user home directory as fallback: {workDir}");
                    }
                }

                // 更新DLP.Path_DLP，以便其他方法使用
                DLP.Path_DLP = ytdlpPath;

                Console.WriteLine($"Final yt-dlp path: {ytdlpPath}");
                Console.WriteLine($"Final working directory: {workDir}");

                var psi = new ProcessStartInfo
                {
                    FileName = ytdlpPath,
                    Arguments = $"--dump-json --no-playlist \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workDir
                };
                
                // Add generic enhancement for special domains
                if (isSpecialDomain)
                {
                    string domainName = "";
                    string refererUrl = "";

                    // Determine domain and corresponding referer
                    if (url.Contains("pornhub"))
                    {
                        domainName = "PornHub";
                        refererUrl = "https://www.pornhub.com/";
                    }
                    else if (url.Contains("xvideos"))
                    {
                        domainName = "XVideos";
                        refererUrl = "https://www.xvideos.com/";
                    }
                    else if (url.Contains("xnxx"))
                    {
                        domainName = "XNXX";
                        refererUrl = "https://www.xnxx.com/";
                    }
                    else if (url.Contains("xhamster"))
                    {
                        domainName = "xHamster";
                        refererUrl = "https://xhamster.com/";
                    }
                    else if (url.Contains("youjizz"))
                    {
                        domainName = "YouJizz";
                        refererUrl = "https://www.youjizz.com/";
                    }
                    else if (url.Contains("youku.com"))
                    {
                        domainName = "优酷";
                        refererUrl = "https://www.youku.com/";
                    }

                    Console.WriteLine($"Detected {domainName} website, applying enhanced processing...");

                    // Add generic user agent
                    psi.Arguments += " --user-agent \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.164 Safari/537.36\"";

                    // Add Cookie support
                    if (Data.NeedCookie || Data.UseCookie == UseCookie.Always)
                    {
                        string cookieArg = " --cookies-from-browser ";
                        switch (Data.CookieType)
                        {
                            case CookieType.Chrome:
                                cookieArg += "chrome";
                                break;
                            case CookieType.Edge:
                                cookieArg += "edge";
                                break;
                            case CookieType.Firefox:
                                cookieArg += "firefox";
                                break;
                            case CookieType.Opera:
                                cookieArg += "opera";
                                break;
                            case CookieType.Safari:
                                cookieArg += "safari";
                                break;
                            case CookieType.Chromium:
                                cookieArg += "chromium";
                                break;
                            case CookieType.Chrome_Beta:
                                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                                var cookiePath = Path.Combine(homeDir, "Library", "Application Support", "Google", "Chrome Beta");
                                cookieArg += $"chrome:{cookiePath}";
                                break;
                            default:
                                cookieArg += "chrome";
                                break;
                        }
                        psi.Arguments += cookieArg;
                        Console.WriteLine($"Using Cookie: {cookieArg}");
                    }
                    else
                    {
                        // Use Chrome's Cookie by default
                        psi.Arguments += " --cookies-from-browser chrome";
                    }

                    // Add geo-verification-proxy parameter, useful for region-restricted content
                    psi.Arguments += " --geo-verification-proxy \"127.0.0.1:8118\"";

                    // Add referer
                    psi.Arguments += $" --referer \"{refererUrl}\"";

                    // Add generic enhanced HTTP headers
                    psi.Arguments += " --add-header \"Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9\"";
                    psi.Arguments += " --add-header \"Accept-Language: en-US,en;q=0.9\"";
                        psi.Arguments += " --add-header \"Sec-Fetch-Mode: navigate\"";
                    psi.Arguments += " --add-header \"Sec-Fetch-Site: same-origin\"";
                    psi.Arguments += " --add-header \"Sec-Fetch-Dest: document\"";
                    psi.Arguments += " --add-header \"Sec-Ch-Ua: \\\"Google Chrome\\\";v=\\\"113\\\", \\\"Chromium\\\";v=\\\"113\\\"\"";
                    psi.Arguments += " --add-header \"Sec-Ch-Ua-Mobile: ?0\"";
                    psi.Arguments += " --add-header \"Sec-Ch-Ua-Platform: \\\"macOS\\\"\"";

                    // Add generic enhanced options to improve compatibility
                    psi.Arguments += " --no-check-certificate";
                    psi.Arguments += " --extractor-retries 5";
                    psi.Arguments += " --ignore-errors";  // Ignore errors and continue processing
                    psi.Arguments += " --force-ipv4";     // Force IPv4 to avoid IPv6 issues
                    psi.Arguments += " --verbose";        // Add more debug information

                    // Special handling for Youku
                    if (domainName == "youku")
                    {
                        Console.WriteLine("Applying Youku special processing parameters...");

                        // Remove generic extractor option because Youku needs a specialized extractor
                        // psi.Arguments += " --force-generic-extractor";

                        // Add Youku-specific parameters
                        psi.Arguments += " --extractor-args \"youku:player_client=android_passenger\"";
                        psi.Arguments += " --add-header \"User-Agent: Mozilla/5.0 (Linux; Android 11; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.91 Mobile Safari/537.36\"";
                        psi.Arguments += " --add-header \"Referer: https://v.youku.com/\"";

                        // Add more debug information
                        psi.Arguments += " --verbose";
                    }
                    else
                    {
                        // Use generic extractor for other websites
                        psi.Arguments += " --force-generic-extractor"; // Try to use generic extractor
                    }

                    // Use user-configured proxy
                    if (!string.IsNullOrWhiteSpace(Data.Proxy))
                    {
                        psi.Arguments += $" --proxy \"{Data.Proxy}\"";
                        Console.WriteLine($"Using proxy: {Data.Proxy}");
                    }

                    // Add debug information
                    Console.WriteLine($"Special website enhancement parameters: {psi.Arguments}");
                }

                using var proc = Process.Start(psi);
                string json = await proc.StandardOutput.ReadToEndAsync();
                string error = await proc.StandardError.ReadToEndAsync();
                
                await proc.WaitForExitAsync();
                
                // If there are errors and no JSON data was obtained
                if (string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine($"Error analyzing URL: {error}");

                    // Provide different prompts for different error types
                    string errorMessage = $"Analysis error: {error}";
                    string suggestion = "";

                    // Check if it's an error that requires login verification
                    if (error.Contains("Sign in to confirm") || error.Contains("sign in") || error.Contains("confirm you're not a bot"))
                    {
                        suggestion = "Login verification required, the system thinks you may be a bot. Please try using browser cookies.";

                        // Automatically enable Cookie
                        if (Data.UseCookie == UseCookie.WhenNeeded || Data.UseCookie == UseCookie.Always)
                        {
                            Data.NeedCookie = true;
                            Console.WriteLine("Login verification required detected, will use Cookie on next attempt");

                            // Re-analyze
                            suggestion += "\n\nAutomatically retrying with Cookie...";
                            infoBox.Text = errorMessage + "\n\n" + suggestion;

                            // Re-analyze after a one-second delay
                            Task.Delay(1000).ContinueWith(_ => {
                                Avalonia.Threading.Dispatcher.UIThread.Post(async () => {
                                    await AnalyzeUrlAsync();
                                });
                            });
                    return;
                }
                        else if (Data.UseCookie == UseCookie.Ask)
                        {
                            // Prompt user whether to use Cookie
                            suggestion += "\n\nDo you want to retry using browser cookies? Please set Cookie options in the Advanced tab.";
                        }
                    }
                    // Check if it's a specific type of error
                    else if (error.Contains("HTTP Error 403") || error.Contains("Forbidden"))
                    {
                        suggestion = "Server access denied, you may need to use a proxy or add cookies.";
                    }
                    else if (error.Contains("HTTP Error 404") || error.Contains("Not Found"))
                    {
                        suggestion = "The requested resource does not exist, please check if the URL is correct.";
                    }
                    else if (error.Contains("Unable to extract") || error.Contains("Unsupported URL"))
                    {
                        suggestion = "Unable to extract video information, yt-dlp may not support this website or the website structure has changed.";
                    }
                    else if (error.Contains("SSL") || error.Contains("certificate"))
                    {
                        suggestion = "SSL certificate verification failed, please try using the --no-check-certificate option.";
                    }
                    else if (error.Contains("timeout") || error.Contains("timed out"))
                    {
                        suggestion = "Connection timeout, please check your network connection or use a proxy.";
                    }

                    // For special domains, provide unified enhanced tips
                    if (isSpecialDomain)
                    {
                        string domainName = "";

                        if (url.Contains("pornhub"))
                        {
                            domainName = "PornHub";
                        }
                        else if (url.Contains("xvideos"))
                        {
                            domainName = "XVideos";
                        }
                        else if (url.Contains("xnxx"))
                        {
                            domainName = "XNXX";
                        }
                        else if (url.Contains("xhamster"))
                        {
                            domainName = "xHamster";
                        }
                        else if (url.Contains("youjizz"))
                        {
                            domainName = "YouJizz";
                        }

                        suggestion += $"\n\nSpecial tips for {domainName} website:\n" +
                                      "1. Make sure you are using the latest version of yt-dlp\n" +
                                      "2. Try setting a proxy server in the Advanced options\n" +
                                      "3. Try using browser cookies (in Advanced options)\n" +
                                      "4. Some videos may have regional restrictions and require a VPN\n" +
                                      "5. If it still fails, try opening the video page in a browser, then try analyzing again";
                    }

                    infoBox.Text = errorMessage + "\n\n" + suggestion;
                    return;
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    infoBox.Text = "Failed to get video information, please check URL or network.";
                    return;
                }
                // Parse JSON
                var jObj = Newtonsoft.Json.Linq.JObject.Parse(json);
                string title = jObj.Value<string>("title") ?? "";
                string desc = jObj.Value<string>("description") ?? "";
                string thumb = jObj.Value<string>("thumbnail") ?? "";
                infoBox.Text = $"Title: {title}\n\n{desc}";
                // Load thumbnail
                if (!string.IsNullOrWhiteSpace(thumb))
                {
                    Console.WriteLine($"开始加载缩略图: {thumb}");

                    // 异步加载缩略图
                    _ = LoadThumbnailAsync(thumb, thumbImg);

                    // 储存缩略图URL
                    Data.Thumbnail = thumb;
                }
                else
                {
                    Console.WriteLine("无缩略图URL");
                }
                // 解析 formats
                Console.WriteLine("开始解析视频格式信息...");
                var formats = jObj["formats"]?.ToObject<List<Format>>() ?? new List<Format>();

                if (formats.Count == 0)
                {
                    Console.WriteLine("警告: 未找到任何格式信息!");

                    // 尝试直接从JSON中获取格式信息
                    try
                    {
                        var formatsArray = jObj["formats"] as Newtonsoft.Json.Linq.JArray;
                        if (formatsArray != null)
                        {
                            Console.WriteLine($"从JSON中找到{formatsArray.Count}个格式项，尝试手动解析");

                            foreach (var formatItem in formatsArray)
                            {
                                try
                                {
                                    Format format = new Format
                                    {
                                        format_id = formatItem["format_id"]?.ToString(),
                                        format_note = formatItem["format_note"]?.ToString(),
                                        ext = formatItem["ext"]?.ToString(),
                                        vcodec = formatItem["vcodec"]?.ToString(),
                                        acodec = formatItem["acodec"]?.ToString(),
                                        width = formatItem["width"] != null ? (int?)formatItem["width"] : null,
                                        height = formatItem["height"] != null ? (int?)formatItem["height"] : null,
                                        fps = formatItem["fps"] != null ? (double?)formatItem["fps"] : null,
                                        tbr = formatItem["tbr"] != null ? (double?)formatItem["tbr"] : null,
                                        format = formatItem["format"]?.ToString()
                                    };

                                    formats.Add(format);
                                    Console.WriteLine($"手动解析格式: {format.format_id} - {format.format_note} ({format.width}x{format.height})");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"手动解析格式项时出错: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"尝试手动解析格式时出错: {ex.Message}");
                    }
                }

                Console.WriteLine($"找到{formats.Count}个格式");
                
                // 解析视频信息
                Video video = new Video();
                video.title = title;
                video.description = desc;
                video.thumbnail = thumb;
                video.id = jObj.Value<string>("id") ?? "";
                video.duration = double.TryParse(jObj.Value<string>("duration") ?? "0", out double dur) ? dur : 0;
                // 将Format列表转换为VideoFormat列表
                video.formats = ConvertToVideoFormats(formats);
                
                // 保存视频信息到Data
                Data.Video = video;
                Data.IsAnalyzed = true;
                
                // 处理格式信息并更新UI
                await ProcessVideoFormatsAsync(video, videoCombo, audioCombo, videoInfo, audioInfo);

                // Get subtitle information
                await FetchSubtitlesAsync(url, subtitleCombo, subtitleInfo);
            }
            catch (Exception ex)
            {
                infoBox.Text = $"Analysis failed: {ex.Message}";
                Console.WriteLine($"Analysis error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // 重置分析状态
                Data.IsAnalyzing = false;

                // 恢复按钮状态
                UpdateButtonState(analyzeButton, false, "Analyze");
            }
        }

        // Get subtitle information
        private async Task FetchSubtitlesAsync(string url, ComboBox? subtitleCombo, TextBlock? subtitleInfo)
        {
            try
            {
                Console.WriteLine("正在获取字幕信息...");
                
                // 构建获取字幕信息的命令
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--list-subs \"{url}\""
                };
                
                // 如果需要自动字幕，添加参数
                if (Data.IncludeAutoSubtitles)
                {
                    psi.Arguments += " --write-auto-sub";
                    Console.WriteLine("包含自动字幕");
                }
                
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                
                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    Console.WriteLine("启动获取字幕进程失败");
                    return;
                }
                
                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
                
                // Parse subtitle information
                var subtitles = ParseSubtitles(output);
                
                // If subtitles are found, populate the dropdown
                if (subtitles.Count > 0)
                {
                    Console.WriteLine($"Found {subtitles.Count} subtitles");
                    
                    // Populate UI
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (subtitleCombo != null)
                        {
                            subtitleCombo.ItemsSource = subtitles;
                            subtitleCombo.SelectedIndex = subtitles.Count > 0 ? 0 : -1;
                            
                            if (subtitleInfo != null && subtitles.Count > 0)
                            {
                                subtitleInfo.Text = $"{subtitles[0].format_id} | {subtitles[0].format_note} | {subtitles[0].ext}";
                            }
                        }
                    });
                }
                else
                {
                    Console.WriteLine("No subtitles found");
                    if (subtitleInfo != null)
                    {
                        subtitleInfo.Text = "No subtitles found";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting subtitles: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        // 解析字幕信息
        private List<Format> ParseSubtitles(string output)
        {
            List<Format> subtitles = new List<Format>();
            
            try
            {
                // 示例正则表达式，可能需要根据实际输出调整
                var regex = new Regex(@"(\w+)\s+(\w+)\s+(\w+)\s+(.+)");
                bool inSubtitleSection = false;
                
                // 解析输出行
                string[] lines = output.Split('\n');
                foreach (var line in lines)
                {
                    // 检查是否进入字幕部分
                    if (line.Contains("Available subtitles"))
                    {
                        inSubtitleSection = true;
                        continue;
                    }
                    
                    // 如果不在字幕部分，继续
                    if (!inSubtitleSection) continue;
                    
                    // 如果行包含语言代码和名称，尝试解析
                    if (line.Trim().Length > 5 && !line.StartsWith("Available") && !line.StartsWith("Language") && !string.IsNullOrWhiteSpace(line))
                    {
                        // 根据空格分割
                        string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            string langCode = parts[0].Trim();
                            string langName = parts.Length > 2 ? string.Join(" ", parts.Skip(1)) : parts[1].Trim();
                            
                            // 创建字幕格式对象
                            Format subtitle = new Format
                            {
                                format_id = langCode,
                                format_note = langName,
                                ext = "srt",
                                type = FormatType.unknown,
                                vcodec = "none",
                                acodec = "none"
                            };
                            
                            subtitles.Add(subtitle);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析字幕信息时出错: {ex.Message}");
            }
            
            return subtitles;
        }

        private void Button_Download(object sender, RoutedEventArgs e)
        {
            // 开始下载
            Download_Start();
        }

        private void Download_Start()
        {
            Console.WriteLine("开始下载视频");

            // 获取下载按钮
            var downloadButton = this.FindControl<Button>("DownloadButton");

            if (string.IsNullOrWhiteSpace(Data.Url))
            {
                Console.WriteLine("URL为空，无法下载");
                return;
            }

            if (!Data.IsAnalyzed)
            {
                Console.WriteLine("请先分析视频");
                return;
            }

            if (Data.IsDownload)
            {
                Console.WriteLine("正在下载中，请稍候");
                return;
            }

            // 更新UI状态 - 下载中
            UpdateButtonState(downloadButton, true, "Downloading...");

            // 设置下载状态
            Data.IsDownload = true;
            Data.IsAbouted = false;

            // 重置下载进度（不要 new 新对象，只重置属性）
            Data.DNStatus_Video.Percent = 0;
            Data.DNStatus_Video.Speed = string.Empty;
            Data.DNStatus_Video.ETA = string.Empty;
            Data.DNStatus_Video.Size = string.Empty;

            Data.DNStatus_Audio.Percent = 0;
            Data.DNStatus_Audio.Speed = string.Empty;
            Data.DNStatus_Audio.ETA = string.Empty;
            Data.DNStatus_Audio.Size = string.Empty;

            // 获取选中的格式
            var selectedFormat = Data.selectedFormat;
            if (selectedFormat == null)
            {
                Console.WriteLine("未选择格式，无法下载");
                Data.IsDownload = false;
                return;
            }

            Console.WriteLine($"选中的格式: {selectedFormat}");

            // 创建目标目录
            try
            {
                if (!Directory.Exists(Data.TargetPath))
                {
                    Directory.CreateDirectory(Data.TargetPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建目录时出错: {ex.Message}");
                Data.IsDownload = false;
                return;
            }

            // 创建输出模板
            string outputTemplate = Path.Combine(Data.TargetPath, "%(title)s.%(ext)s");

            // 创建DLP实例
            var dlp = new DLP
            {
                Url = Data.Url,
                Options = new Dictionary<string, string>
                {
                    { "format", "bestvideo+bestaudio/best" },
                    { "output", outputTemplate },
                    { "no-playlist", "" },
                    { "merge-output-format", "mp4" },  // 强制合并为mp4格式
                    { "remux-video", "mp4" },          // 重新封装视频为mp4
                    { "embed-metadata", "" },          // 嵌入元数据
                    { "embed-chapters", "" },          // 嵌入章节信息
                    { "embed-subs", "" }               // 嵌入字幕
                }
            };

            // 确保ffmpeg路径正确设置
            if (string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG))
            {
                // 尝试查找ffmpeg
                string depsDir = Path.Combine(App.AppPath, "deps");
                if (Directory.Exists(depsDir))
                {
                    string ffmpegPath = Path.Combine(depsDir, "ffmpeg");
                    if (File.Exists(ffmpegPath))
                    {
                        FFMPEG.Path_FFMPEG = ffmpegPath;
                        Console.WriteLine($"Found ffmpeg at: {ffmpegPath}");
                    }
                }

                // 如果仍然找不到，尝试使用系统ffmpeg
                if (string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG))
                {
                    FFMPEG.Path_FFMPEG = "/usr/local/bin/ffmpeg";
                    Console.WriteLine("Using system ffmpeg at: /usr/local/bin/ffmpeg");
                }
            }

            // 添加ffmpeg路径
            if (!string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG) && File.Exists(FFMPEG.Path_FFMPEG))
            {
                dlp.Options["ffmpeg-location"] = FFMPEG.Path_FFMPEG;
                Console.WriteLine($"Using ffmpeg at: {FFMPEG.Path_FFMPEG}");
            }

            // 添加高级选项
            // 代理设置
            if (!string.IsNullOrWhiteSpace(Data.Proxy))
            {
                dlp.Options["proxy"] = Data.Proxy;
            }
            
            // Cookie settings
            if (Data.NeedCookie || Data.UseCookie == UseCookie.Always)
            {
                dlp.Cookie(Data.CookieType, true);
                Console.WriteLine($"Using Cookie for download: {Data.CookieType}");
            }

            // Subtitle settings
            if (!string.IsNullOrWhiteSpace(Data.SubLang))
            {
                dlp.Options["sub-langs"] = Data.SubLang;
                dlp.Options["write-auto-sub"] = "";
            }
            
            // Extra parameters
            if (!string.IsNullOrWhiteSpace(Data.ExtraArgs))
            {
                string[] extraArgs = Data.ExtraArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string arg in extraArgs)
                {
                    string cleanArg = arg.Trim();
                    if (cleanArg.StartsWith("--"))
                    {
                        string[] parts = cleanArg.Substring(2).Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            // Use indexer syntax instead of dictionary methods to avoid potential key duplication issues
                            dlp.Options[parts[0]] = parts[1];
                        }
                        else
                        {
                            // Use indexer syntax instead of dictionary methods to avoid potential key duplication issues
                            dlp.Options[parts[0]] = "";
                        }
                    }
                }
            }

            // Add thumbnail download options
            if (Data.DownloadThumbnail)
            {
                dlp.Options["write-thumbnail"] = "";
                dlp.Options["convert-thumbnails"] = "jpg";
            }

            // If yt-dlp path is not set, use the system yt-dlp
            if (string.IsNullOrWhiteSpace(DLP.Path_DLP))
            {
                DLP.Path_DLP = "/usr/local/bin/yt-dlp";
            }

            Console.WriteLine($"执行yt-dlp命令: {dlp.Args}");

            // 添加到运行列表
            RunningDLP.Add(dlp);

            // 正则表达式，用于解析下载进度
            var regexProgress = new Regex(@"\[download\]\s+(?<percent>[\d\.]+)%\s+of\s+(?<size>[\d\.]+(?:K|M|G)i?B)(?:\s+at\s+(?<speed>[\d\.]+(?:K|M|G)i?B/s))?(?:\s+ETA\s+(?<eta>[\d:]+))?");
            // 备用正则表达式，用于匹配不同格式的输出
            var regexProgressAlt = new Regex(@"\[download\]\s+(?<percent>[\d\.]+)%\s+of\s+~?(?<size>[\d\.]+(?:K|M|G)i?B)(?:\s+at\s+(?<speed>[\d\.]+(?:K|M|G)i?B/s))?(?:\s+ETA\s+(?<eta>[\d:]+))?");
            // 另一种格式的正则表达式
            var regexProgressAlt2 = new Regex(@"\[download\]\s+(?<percent>[\d\.]+)%(?:\s+of\s+~?(?<size>[\d\.]+(?:K|M|G)i?B))?(?:\s+at\s+(?<speed>[\d\.]+(?:K|M|G)i?B/s))?(?:\s+ETA\s+(?<eta>[\d:]+))?");
            var regexMerging = new Regex(@"\[Merger\]\s+Merging formats into");
            var regexDestination = new Regex(@"\[download\]\s+Destination:\s+(?<filename>.+)");
            var regexFinished = new Regex(@"\[download\]\s+(?<filename>.+)\s+has already been downloaded");
            var regexDeleting = new Regex(@"\[ExtractAudio\]\s+Deleting original file");

            // 执行命令并处理输出
            Task.Run(() =>
            {
                try
                {
                    dlp.Exec(
                        stdall: (line) =>
                        {
                            // 只输出关键进度和错误
                            var matchProgress = regexProgress.Match(line);
                            var matchProgressAlt = regexProgressAlt.Match(line);
                            var matchProgressAlt2 = regexProgressAlt2.Match(line);

                            if (matchProgress.Success || matchProgressAlt.Success || matchProgressAlt2.Success)
                            {
                                var groups = matchProgress.Success ? matchProgress.Groups :
                                            (matchProgressAlt.Success ? matchProgressAlt.Groups : matchProgressAlt2.Groups);

                                if (groups["percent"].Success && double.TryParse(groups["percent"].Value, out double percent))
                                {
                                    Console.WriteLine($"下载进度: {percent}%");
                                }

                                // 在UI线程上更新进度
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    if (groups["percent"].Success && double.TryParse(groups["percent"].Value, out double percent2))
                                    {
                                        Data.DNStatus_Video.Percent = percent2;
                                    }

                                    if (groups["size"].Success)
                                    {
                                        Data.DNStatus_Video.Size = groups["size"].Value;
                                    }

                                    if (groups["speed"].Success)
                                    {
                                        Data.DNStatus_Video.Speed = groups["speed"].Value;
                                    }
                                    else
                                    {
                                        Data.DNStatus_Video.Speed = string.Empty;
                                    }

                                    if (groups["eta"].Success)
                                    {
                                        Data.DNStatus_Video.ETA = groups["eta"].Value;
                                    }
                                    else
                                    {
                                        Data.DNStatus_Video.ETA = string.Empty;
                                    }
                                });

                                return;
                            }

                            // 只输出合并、提取、完成等关键状态
                            var matchMerging = regexMerging.Match(line);
                            if (matchMerging.Success)
                            {
                                Console.WriteLine("正在合并视频和音频...");
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    Data.DNStatus_Video.Percent = 99;
                                    Data.DNStatus_Video.Speed = string.Empty;
                                    Data.DNStatus_Video.ETA = string.Empty;
                                    Data.DNStatus_Video.Size = "正在合并视频和音频...";
                                });
                                return;
                            }

                            var matchDeleting = regexDeleting.Match(line);
                            if (matchDeleting.Success)
                            {
                                Console.WriteLine("正在提取音频...");
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    Data.DNStatus_Video.Percent = 99;
                                    Data.DNStatus_Video.Speed = string.Empty;
                                    Data.DNStatus_Video.ETA = string.Empty;
                                    Data.DNStatus_Video.Size = "正在提取音频...";
                                });
                                return;
                            }

                            var matchFinished = regexFinished.Match(line);
                            if (matchFinished.Success)
                            {
                                Console.WriteLine("文件已存在，跳过下载");
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    Data.DNStatus_Video.Percent = 100;
                                    Data.DNStatus_Video.Speed = string.Empty;
                                    Data.DNStatus_Video.ETA = string.Empty;
                                    Data.DNStatus_Video.Size = "文件已存在，跳过下载";
                                });
                                return;
                            }

                            var matchDestination = regexDestination.Match(line);
                            if (matchDestination.Success)
                            {
                                var filename = matchDestination.Groups["filename"].Value;
                                Console.WriteLine($"正在下载: {Path.GetFileName(filename)}");
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    Data.DNStatus_Video.Size = $"正在下载: {Path.GetFileName(filename)}";
                                });
                                return;
                            }
                        },
                        stdout: null,
                        stderr: (line) => { Console.WriteLine($"错误: {line}"); }
                    );

                    // 下载完成
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        Data.IsDownload = false;
                        RunningDLP.Remove(dlp);

                        // 恢复下载按钮状态
                        var downloadButton = this.FindControl<Button>("DownloadButton");
                        UpdateButtonState(downloadButton, false, "Download");

                        if (!Data.IsAbouted)
                        {
                            Console.WriteLine("下载完成");

                            // 更新下载状态
                            Data.DNStatus_Video.Percent = 100;
                            Data.DNStatus_Video.Speed = string.Empty;
                            Data.DNStatus_Video.ETA = string.Empty;
                            Data.DNStatus_Video.Size = "下载完成";

                            // 尝试查找下载的视频文件
                            string downloadedFilePath = FindDownloadedVideoFile(Data.TargetPath, Data.Video?.title);
                            if (!string.IsNullOrEmpty(downloadedFilePath))
                            {
                                Console.WriteLine($"找到下载的视频文件: {downloadedFilePath}");
                                // 加载视频播放器
                                LoadVideoPlayer(downloadedFilePath);
                            }

                            // 播放通知声音
                            Libs.Util.NotifySound(Data.PathNotify);

                            // 打开文件夹
                            if (Directory.Exists(Data.TargetPath) && Data.OpenFolderAfterDownload)
                            {
                                Libs.Util.OpenFolder(Data.TargetPath);
                            }
                        }
                        else
                        {
                            Console.WriteLine("下载已取消");

                            // 更新下载状态
                            Data.DNStatus_Video.Percent = 0;
                            Data.DNStatus_Video.Speed = string.Empty;
                            Data.DNStatus_Video.ETA = string.Empty;
                            Data.DNStatus_Video.Size = "下载已取消";
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"下载过程中发生错误: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        Data.IsDownload = false;
                        RunningDLP.Remove(dlp);

                        // 恢复下载按钮状态
                        var downloadButton = this.FindControl<Button>("DownloadButton");
                        UpdateButtonState(downloadButton, false, "Download");

                        // 更新下载状态
                        Data.DNStatus_Video.Percent = 0;
                        Data.DNStatus_Video.Speed = string.Empty;
                        Data.DNStatus_Video.ETA = string.Empty;
                        Data.DNStatus_Video.Size = $"下载出错: {ex.Message}";
                    });
                }
            });
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            // 取消下载
            if (Data.IsDownload)
            {
                Data.IsAbouted = true;
                foreach (var dlp in RunningDLP)
                {
                    dlp.Close();
                }
            }
        }

        private async void Button_Browse(object sender, RoutedEventArgs e)
        {
            // 浏览文件夹
            try
            {
                // 创建文件夹选择对话框
                var dialog = new Avalonia.Controls.OpenFolderDialog
                {
                    Title = "选择保存位置",
                    Directory = Data.TargetPath
                };

                // 显示对话框
                var result = await dialog.ShowAsync(this);

                // 如果用户选择了文件夹
                if (!string.IsNullOrWhiteSpace(result))
                {
                    // 更新目标路径
                    Data.TargetPath = result;

                    // 如果已经分析了视频，更新目标文件名
                    if (Data.IsAnalyzed && Data.Video != null)
                    {
                        // 获取当前选择的格式
                        Format? selectedFormat = null;
                        var videoCombo = this.FindControl<ComboBox>("VideoFormatComboBox");
                        if (videoCombo != null && videoCombo.SelectedItem is Format fmt)
                        {
                            selectedFormat = fmt;
                        }

                        // 更新目标文件名显示
                        if (selectedFormat != null)
                        {
                            UpdateTargetFileName(Data.TargetPath, Data.FilenameTemplate, selectedFormat.ext);
                        }
                    }

                    // 更新保存路径文本框
                    var savePathBox = this.FindControl<TextBox>("SavePathTextBox");
                    if (savePathBox != null)
                    {
                        savePathBox.Text = Data.TargetPath;
                    }

                    Console.WriteLine($"Save location selected: {Data.TargetPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"选择文件夹时出错: {ex.Message}");
            }
        }

        // 下载指定格式
        private void Download_Format(Format format)
        {
            try
            {
                // 检查格式是否为null
                if (format == null)
                {
                    ShowErrorMessage("无法下载：未选择格式或格式无效");
                    Console.WriteLine("下载错误: 格式为null");
                    return;
                }

                // 检查URL是否有效
                if (string.IsNullOrWhiteSpace(Data.Url))
                {
                    ShowErrorMessage("无法下载：URL为空");
                    Console.WriteLine("下载错误: URL为空");
                    return;
                }

                // 检查是否已分析
                if (!Data.IsAnalyzed)
                {
                    ShowErrorMessage("请先分析视频");
                    Console.WriteLine("下载错误: 未分析视频");
                    return;
                }

                // 检查format_id是否有效
                if (string.IsNullOrWhiteSpace(format.format_id))
                {
                    ShowErrorMessage("无法下载：格式ID无效");
                    Console.WriteLine("下载错误: 格式ID为空");
                    return;
                }

            Console.WriteLine($"下载格式: {format.format_id} {format.format_note}");
            
            // 初始化下载组件
            var dlp = new Wrappers.DLP();
            
            // 设置URL
            dlp.Url = Data.Url;
            
            // 设置代理（如果有）
            if (!string.IsNullOrWhiteSpace(Data.Proxy))
            {
                dlp.Options["proxy"] = Data.Proxy;
            }

                // Cookie设置
                if (Data.NeedCookie || Data.UseCookie == UseCookie.Always)
                {
                    dlp.Cookie(Data.CookieType, true);
                    Console.WriteLine($"下载格式时使用Cookie: {Data.CookieType}");
                }
            
            // 设置格式
            if (format.vcodec != "none" || format.acodec != "none")
                {
                    // 如果是纯视频格式，添加最佳音频
                    if (format.vcodec != "none" && format.acodec == "none")
                    {
                        dlp.Options["format"] = $"{format.format_id}+bestaudio/best";
                        Console.WriteLine($"下载视频格式 {format.format_id} 并添加最佳音频");
                    }
                    // 如果是纯音频格式，只下载音频
                    else if (format.vcodec == "none" && format.acodec != "none")
            {
                dlp.Options["format"] = format.format_id;
                        dlp.Options["extract-audio"] = "";
                        Console.WriteLine($"下载音频格式 {format.format_id}");
                    }
                    // 如果是组合格式，直接使用
                    else
                    {
                        dlp.Options["format"] = format.format_id;
                        Console.WriteLine($"下载组合格式 {format.format_id}");
                    }

                    // 添加合并选项
                    if (format.vcodec != "none")
                    {
                        dlp.Options["merge-output-format"] = "mp4";
                        dlp.Options["remux-video"] = "mp4";
                        dlp.Options["embed-metadata"] = "";
                        dlp.Options["embed-chapters"] = "";
                        dlp.Options["embed-subs"] = "";

                        // 确保ffmpeg路径正确设置
                        if (string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG))
                        {
                            // 尝试查找ffmpeg
                            string depsDir = Path.Combine(App.AppPath, "deps");
                            if (Directory.Exists(depsDir))
                            {
                                string ffmpegPath = Path.Combine(depsDir, "ffmpeg");
                                if (File.Exists(ffmpegPath))
                                {
                                    FFMPEG.Path_FFMPEG = ffmpegPath;
                                    Console.WriteLine($"Found ffmpeg at: {ffmpegPath}");
                                }
                            }

                            // 如果仍然找不到，尝试使用系统ffmpeg
                            if (string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG))
                            {
                                FFMPEG.Path_FFMPEG = "/usr/local/bin/ffmpeg";
                                Console.WriteLine("Using system ffmpeg at: /usr/local/bin/ffmpeg");
                            }
                        }

                        // 添加ffmpeg路径
                        if (!string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG) && File.Exists(FFMPEG.Path_FFMPEG))
                        {
                            dlp.Options["ffmpeg-location"] = FFMPEG.Path_FFMPEG;
                            Console.WriteLine($"Using ffmpeg at: {FFMPEG.Path_FFMPEG}");
                        }
                    }
                }

                // Set output template
            if (!string.IsNullOrWhiteSpace(Data.FilenameTemplate))
            {
                    // Create complete output path
                string outputTemplate = Path.Combine(Data.TargetPath, Data.FilenameTemplate);
                dlp.Options["output"] = outputTemplate;
            }
            
                // If it's a subtitle, set special parameters
            if (format.vcodec == "none" && format.acodec == "none")
            {
                dlp.Options["skip-download"] = "";
                dlp.Options["write-sub"] = "";

                    // 确保字幕语言代码有效
                    if (string.IsNullOrWhiteSpace(format.format_id))
                    {
                        ShowErrorMessage("无法下载字幕：语言代码无效");
                        Console.WriteLine("字幕下载错误: 语言代码为空");
                        return;
                    }

                    // Set subtitle language
                dlp.Options["sub-langs"] = format.format_id;
                
                    // If auto-generated subtitles should be included
                if (Data.IncludeAutoSubtitles && format.format_note?.Contains("auto") == true)
                {
                    dlp.Options["write-auto-sub"] = "";
                }
                
                    Console.WriteLine($"Setting subtitle download: {format.format_id}");
            }
            
                // Add thumbnail download options
            if (Data.DownloadThumbnail)
            {
                dlp.Options["write-thumbnail"] = "";
                dlp.Options["convert-thumbnails"] = "jpg";
                    Console.WriteLine("Enabling thumbnail download");
            }
            
                // Extra parameters
            if (!string.IsNullOrWhiteSpace(Data.ExtraArgs))
            {
                string[] extraArgs = Data.ExtraArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string arg in extraArgs)
                {
                    string cleanArg = arg.Trim();
                    if (cleanArg.StartsWith("--"))
                    {
                        string[] parts = cleanArg.Substring(2).Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            // 使用索引器语法替代字典方法，避免潜在的键重复问题
                            dlp.Options[parts[0]] = parts[1];
                        }
                        else
                        {
                            // 对于没有值的参数，设置空字符串
                            dlp.Options[parts[0]] = "";
                        }
                    }
                }
            }
            
            // 开始下载
            RunningDLP.Add(dlp);
            
            // 设置正则表达式，用于解析下载进度
            var regexProgress = new Regex(@"\[download\]\s+(?<percent>[\d\.]+)%\s+of\s+(?<size>[\d\.]+(?:K|M|G)i?B)(?:\s+at\s+(?<speed>[\d\.]+(?:K|M|G)i?B/s))?(?:\s+ETA\s+(?<eta>[\d:]+))?");
                var regexError = new Regex(@"ERROR:.*?(?<error>.+)");
            
            // 执行下载并监听输出
            dlp.Exec(
                stdall: (line) => {
                        // 检查错误信息
                        var errorMatch = regexError.Match(line);
                        if (errorMatch.Success)
                        {
                            string errorMsg = errorMatch.Groups["error"].Value;
                            Console.WriteLine($"下载错误: {errorMsg}");

                            // 在UI线程上显示错误
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                ShowErrorMessage($"下载错误: {errorMsg}");
                            });

                            return;
                        }

                    // 解析下载进度
                    var match = regexProgress.Match(line);
                    if (match.Success)
                    {
                        if (match.Groups["percent"].Success && double.TryParse(match.Groups["percent"].Value, out double percent))
                        {
                            Console.WriteLine($"下载进度: {percent}%");
                            
                            // 更新UI
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                // 根据格式类型更新不同的状态
                                if (format.vcodec != "none" && format.acodec == "none")
                                {
                                    // 更新视频下载状态
                                    Data.DNStatus_Video.Percent = percent;
                                    if (match.Groups["speed"].Success)
                                        Data.DNStatus_Video.Speed = match.Groups["speed"].Value;
                                    if (match.Groups["eta"].Success)
                                        Data.DNStatus_Video.ETA = match.Groups["eta"].Value;
                                    if (match.Groups["size"].Success)
                                        Data.DNStatus_Video.Size = match.Groups["size"].Value;
                                }
                                else if (format.vcodec == "none" && format.acodec != "none")
                                {
                                    // 更新音频下载状态
                                    Data.DNStatus_Audio.Percent = percent;
                                    if (match.Groups["speed"].Success)
                                        Data.DNStatus_Audio.Speed = match.Groups["speed"].Value;
                                    if (match.Groups["eta"].Success)
                                        Data.DNStatus_Audio.ETA = match.Groups["eta"].Value;
                                    if (match.Groups["size"].Success)
                                        Data.DNStatus_Audio.Size = match.Groups["size"].Value;
                                }
                                else
                                {
                                    // 更新综合下载状态
                                    Data.DNStatus_Video.Percent = percent;
                                    if (match.Groups["speed"].Success)
                                        Data.DNStatus_Video.Speed = match.Groups["speed"].Value;
                                    if (match.Groups["eta"].Success)
                                        Data.DNStatus_Video.ETA = match.Groups["eta"].Value;
                                    if (match.Groups["size"].Success)
                                        Data.DNStatus_Video.Size = match.Groups["size"].Value;
                                }
                            });
                        }
                    }
                    },
                    stderr: (error) => {
                        // 处理错误输出
                        Console.WriteLine($"下载错误输出: {error}");

                        // 在UI线程上显示错误
                        if (!string.IsNullOrWhiteSpace(error) && error.Contains("ERROR"))
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                ShowErrorMessage($"下载错误: {error}");
                            });
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                // 捕获并处理所有异常
                Console.WriteLine($"下载过程中发生异常: {ex.Message}");
                Console.WriteLine(ex.StackTrace);

                // 在UI线程上显示错误
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    ShowErrorMessage($"下载过程中发生异常: {ex.Message}");
                });
            }
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        private async void ShowErrorMessage(string message)
        {
            // 在控制台输出错误
            Console.WriteLine(message);

            // 更新UI状态
            var infoBox = this.FindControl<TextBox>("VideoInfoBox");
            if (infoBox != null)
            {
                infoBox.Text += $"\n\n错误: {message}";
            }

            // 使用简单的方式显示错误信息
            // 在UI线程上更新状态栏或其他UI元素
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                // 如果有状态栏，可以在这里更新
                Console.WriteLine($"UI线程显示错误: {message}");
            });

            // 如果需要，可以在这里添加更多的错误处理逻辑
            // 例如记录到日志文件等
        }

        private void ResetAdvancedOptions()
        {
            // Reset advanced options
            Data.ExtraArgs = string.Empty;
            Data.Proxy = string.Empty;
            Data.SubLang = "en.*,ja.*,zh.*";
            
            // Update UI
            var extraArgsBox = this.FindControl<TextBox>("ExtraArgsTextBox");
            var proxyBox = this.FindControl<TextBox>("ProxyTextBox");
            var subtitleLangBox = this.FindControl<TextBox>("SubtitleLangTextBox");
            
            if (extraArgsBox != null) extraArgsBox.Text = string.Empty;
            if (proxyBox != null) proxyBox.Text = string.Empty;
            if (subtitleLangBox != null) subtitleLangBox.Text = Data.SubLang;
            
            Console.WriteLine("Advanced options have been reset");
        }

        // Media player related fields
        private Avalonia.Controls.NativeControlHost? _mediaPlayerHost;
        private bool _isMediaPlaying = false;
        private bool _isMuted = false;
        private string _currentMediaUrl = string.Empty;

        // Media player methods
        private async Task PreviewMediaAsync()
        {
            // 如果有本地视频文件路径，则打开视频文件
            if (!string.IsNullOrEmpty(_currentMediaUrl) && File.Exists(_currentMediaUrl))
            {
                OpenVideoFile(_currentMediaUrl);
                return;
            }

            if (string.IsNullOrEmpty(Data.Url) || !Data.IsAnalyzed)
            {
                Console.WriteLine("No video analyzed yet");
                return;
            }

            try
            {
                // Get container for media player
                var container = this.FindControl<Panel>("MediaPlayerContainer");
                var mediaDurationText = this.FindControl<TextBlock>("MediaDurationText");

                if (container == null)
                {
                    Console.WriteLine("Media player container not found");
                    return;
                }

                // Show loading indicator
                mediaDurationText.Text = "Loading preview...";

                // Get best format for preview (low resolution for faster loading)
                var previewFormat = GetPreviewFormat();
                if (previewFormat == null)
                {
                    Console.WriteLine("No suitable preview format found");
                    mediaDurationText.Text = "No preview available";
                    return;
                }

                // Get direct URL for the format
                string directUrl = await GetDirectMediaUrlAsync(Data.Url, previewFormat.format_id);
                if (string.IsNullOrEmpty(directUrl))
                {
                    Console.WriteLine("Failed to get direct media URL");
                    mediaDurationText.Text = "Preview not available";
                    return;
                }

                _currentMediaUrl = directUrl;

                // Create media player if not exists
                if (_mediaPlayerHost == null)
                {
                    _mediaPlayerHost = new Avalonia.Controls.NativeControlHost();

                    // On macOS, we would typically use AVPlayer
                    // This is a placeholder - actual implementation would require platform-specific code
                    // using AVFoundation framework through native interop

                    // For now, we'll just show a message that media preview is not implemented
                    mediaDurationText.Text = "Media preview not implemented";

                    // In a real implementation, we would:
                    // 1. Create an AVPlayer instance
                    // 2. Set up the player with the URL
                    // 3. Create an AVPlayerLayer
                    // 4. Add the layer to a view
                    // 5. Add the view to our NativeControlHost

                    // container.Children.Add(_mediaPlayerHost);
                }

                // Update UI to show we're in preview mode
                var playPauseBtn = this.FindControl<Button>("PlayPauseButton");
                if (playPauseBtn != null)
                {
                    playPauseBtn.Content = "⏸";
                    _isMediaPlaying = true;
                }

                // Update duration text
                if (Data.Video != null && Data.Video.duration > 0)
                {
                    TimeSpan duration = TimeSpan.FromSeconds(Data.Video.duration);
                    mediaDurationText.Text = $"Duration: {duration:hh\\:mm\\:ss}";
                }
                else
                {
                    mediaDurationText.Text = "Duration: Unknown";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error previewing media: {ex.Message}");
                var mediaDurationText = this.FindControl<TextBlock>("MediaDurationText");
                if (mediaDurationText != null)
                {
                    mediaDurationText.Text = $"Preview error: {ex.Message}";
                }
            }
        }

        private Format GetPreviewFormat()
        {
            // Try to find a suitable format for preview
            // Prefer formats with both video and audio, medium resolution

            // Get all formats
            var formats = Data.Video?.formats;
            if (formats == null || formats.Count == 0)
                return null;

            // Convert VideoFormat to Format
            List<Format> convertedFormats = new List<Format>();
            foreach (var vf in formats)
            {
                Format f = new Format
                {
                    format_id = vf.format_id ?? "",
                    format_note = vf.format_note ?? "",
                    ext = vf.ext ?? "",
                    acodec = vf.acodec ?? "",
                    vcodec = vf.vcodec ?? "",
                    url = vf.url ?? "",
                    width = vf.width,
                    height = vf.height,
                    fps = vf.fps,
                    format = vf.format ?? ""
                };
                convertedFormats.Add(f);
            }

            // First try to find a format with both video and audio, around 480p
            var previewFormat = convertedFormats.FirstOrDefault(f =>
                f.vcodec != null && f.vcodec != "none" &&
                f.acodec != null && f.acodec != "none" &&
                f.height >= 360 && f.height <= 480);

            // If not found, try any format with both video and audio
            if (previewFormat == null)
            {
                previewFormat = convertedFormats.FirstOrDefault(f =>
                    f.vcodec != null && f.vcodec != "none" &&
                    f.acodec != null && f.acodec != "none");
            }

            // If still not found, use any video format
            if (previewFormat == null)
            {
                previewFormat = convertedFormats.FirstOrDefault(f =>
                    f.vcodec != null && f.vcodec != "none");
            }

            return previewFormat;
        }

        private async Task<string> GetDirectMediaUrlAsync(string videoUrl, string formatId)
        {
            try
            {
                // Use yt-dlp to get direct URL for the format
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"-f {formatId} -g \"{videoUrl}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                // The output should be the direct URL
                return output.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting direct media URL: {ex.Message}");
                return string.Empty;
            }
        }

        private void ToggleMediaPlayback()
        {
            // Toggle play/pause state
            _isMediaPlaying = !_isMediaPlaying;

            // Update button text
            var playPauseBtn = this.FindControl<Button>("PlayPauseButton");
            if (playPauseBtn != null)
            {
                playPauseBtn.Content = _isMediaPlaying ? "⏸" : "▶";
            }

            // 如果有本地视频文件路径，则打开视频文件
            if (!string.IsNullOrEmpty(_currentMediaUrl) && File.Exists(_currentMediaUrl))
            {
                OpenVideoFile(_currentMediaUrl);
                return;
            }

            // In a real implementation, we would call the native player's play/pause methods
        }

        private void ToggleMute()
        {
            // Toggle mute state
            _isMuted = !_isMuted;

            // Update button text
            var muteBtn = this.FindControl<Button>("MuteButton");
            if (muteBtn != null)
            {
                muteBtn.Content = _isMuted ? "🔇" : "🔊";
            }

            // In a real implementation, we would call the native player's mute methods
        }

        private void SeekMedia(double position)
        {
            // In a real implementation, we would seek the native player to the specified position
            Console.WriteLine($"Seeking to position: {position}");
        }

        /// <summary>
        /// 查找下载的视频文件
        /// </summary>
        /// <param name="targetPath">目标文件夹路径</param>
        /// <param name="videoTitle">视频标题</param>
        /// <returns>找到的视频文件路径，如果未找到则返回空字符串</returns>
        private string FindDownloadedVideoFile(string targetPath, string videoTitle)
        {
            try
            {
                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                {
                    Console.WriteLine("目标文件夹不存在或为空");
                    return string.Empty;
                }

                if (string.IsNullOrEmpty(videoTitle))
                {
                    Console.WriteLine("视频标题为空，无法查找文件");
                    return string.Empty;
                }

                // 清理视频标题，移除不允许在文件名中使用的字符
                string safeTitle = new string(videoTitle.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());

                // 获取目标文件夹中的所有视频文件
                string[] videoFiles = Directory.GetFiles(targetPath, "*.mp4")
                    .Concat(Directory.GetFiles(targetPath, "*.mkv"))
                    .Concat(Directory.GetFiles(targetPath, "*.webm"))
                    .Concat(Directory.GetFiles(targetPath, "*.mov"))
                    .ToArray();

                Console.WriteLine($"在目标文件夹中找到 {videoFiles.Length} 个视频文件");

                // 首先尝试精确匹配
                foreach (string file in videoFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.Equals(safeTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"找到精确匹配的视频文件: {file}");
                        return file;
                    }
                }

                // 如果没有精确匹配，尝试部分匹配
                foreach (string file in videoFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.Contains(safeTitle, StringComparison.OrdinalIgnoreCase) ||
                        safeTitle.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"找到部分匹配的视频文件: {file}");
                        return file;
                    }
                }

                // 如果仍然没有找到，返回最新的视频文件
                if (videoFiles.Length > 0)
                {
                    string newestFile = videoFiles.OrderByDescending(f => new FileInfo(f).CreationTime).First();
                    Console.WriteLine($"未找到匹配的视频文件，返回最新的视频文件: {newestFile}");
                    return newestFile;
                }

                Console.WriteLine("未找到任何视频文件");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查找视频文件时出错: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 加载视频播放器
        /// </summary>
        /// <param name="videoFilePath">视频文件路径</param>
        private void LoadVideoPlayer(string videoFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(videoFilePath) || !File.Exists(videoFilePath))
                {
                    Console.WriteLine("视频文件不存在或路径为空");
                    return;
                }

                Console.WriteLine($"加载视频播放器，文件路径: {videoFilePath}");

                // 获取媒体播放器容器
                var container = this.FindControl<Panel>("MediaPlayerContainer");
                var thumbImg = this.FindControl<Image>("ThumbnailImage");
                var mediaDurationText = this.FindControl<TextBlock>("MediaDurationText");

                if (container == null || thumbImg == null)
                {
                    Console.WriteLine("找不到媒体播放器容器或缩略图控件");
                    return;
                }

                // 显示加载指示器
                mediaDurationText.Text = "正在加载视频...";

                // 创建媒体播放器（如果不存在）
                if (_mediaPlayerHost == null)
                {
                    _mediaPlayerHost = new Avalonia.Controls.NativeControlHost();
                }

                // 在macOS上，我们通常使用AVPlayer
                // 这里是一个占位符 - 实际实现需要通过本机互操作使用AVFoundation框架

                // 由于我们无法直接在Avalonia中使用AVFoundation，
                // 我们将使用一个简单的方法来显示视频的第一帧作为预览

                // 使用ffmpeg提取视频的第一帧作为预览图像
                string previewImagePath = ExtractVideoPreviewImage(videoFilePath);

                if (!string.IsNullOrEmpty(previewImagePath) && File.Exists(previewImagePath))
                {
                    // 加载预览图像
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            using var fileStream = new FileStream(previewImagePath, FileMode.Open, FileAccess.Read);
                            var bitmap = new Bitmap(fileStream);
                            thumbImg.Source = bitmap;

                            // 更新UI状态 - 使用更醒目的提示
                            mediaDurationText.Text = "✅ 下载完成 - 点击播放按钮观看";
                            mediaDurationText.Foreground = new SolidColorBrush(Color.Parse("#4CAF50")); // 使用绿色
                            mediaDurationText.FontWeight = FontWeight.Bold;

                            // 更新播放按钮状态
                            var playPauseBtn = this.FindControl<Button>("PlayPauseButton");
                            if (playPauseBtn != null)
                            {
                                playPauseBtn.Content = "▶";
                                _isMediaPlaying = false;

                                // 更新按钮点击事件，使其打开视频文件
                                playPauseBtn.Click -= (s, e) => ToggleMediaPlayback();
                                playPauseBtn.Click += (s, e) => OpenVideoFile(videoFilePath);

                                // 使播放按钮更醒目
                                playPauseBtn.Background = new SolidColorBrush(Color.Parse("#4CAF50")); // 使用绿色背景
                                playPauseBtn.Foreground = new SolidColorBrush(Colors.White); // 白色文字
                            }

                            // 存储当前视频文件路径
                            _currentMediaUrl = videoFilePath;

                            Console.WriteLine("视频预览图像已加载");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"加载视频预览图像时出错: {ex.Message}");
                            mediaDurationText.Text = "无法加载视频预览";
                        }
                    });
                }
                else
                {
                    Console.WriteLine("无法提取视频预览图像");
                    mediaDurationText.Text = "✅ 下载完成 - 点击播放按钮观看";
                    mediaDurationText.Foreground = new SolidColorBrush(Color.Parse("#4CAF50")); // 使用绿色
                    mediaDurationText.FontWeight = FontWeight.Bold;

                    // 更新播放按钮状态，使其打开视频文件
                    var playPauseBtn = this.FindControl<Button>("PlayPauseButton");
                    if (playPauseBtn != null)
                    {
                        playPauseBtn.Content = "▶";
                        _isMediaPlaying = false;

                        // 更新按钮点击事件，使其打开视频文件
                        playPauseBtn.Click -= (s, e) => ToggleMediaPlayback();
                        playPauseBtn.Click += (s, e) => OpenVideoFile(videoFilePath);

                        // 使播放按钮更醒目
                        playPauseBtn.Background = new SolidColorBrush(Color.Parse("#4CAF50")); // 使用绿色背景
                        playPauseBtn.Foreground = new SolidColorBrush(Colors.White); // 白色文字
                    }

                    // 存储当前视频文件路径
                    _currentMediaUrl = videoFilePath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载视频播放器时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 使用ffmpeg提取视频的第一帧作为预览图像
        /// </summary>
        /// <param name="videoFilePath">视频文件路径</param>
        /// <returns>预览图像文件路径，如果提取失败则返回空字符串</returns>
        private string ExtractVideoPreviewImage(string videoFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(videoFilePath) || !File.Exists(videoFilePath))
                {
                    Console.WriteLine("视频文件不存在或路径为空");
                    return string.Empty;
                }

                // 确保ffmpeg路径正确设置
                if (string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG) || !File.Exists(FFMPEG.Path_FFMPEG))
                {
                    // 尝试查找ffmpeg
                    string depsDir = Path.Combine(App.AppPath, "deps");
                    if (Directory.Exists(depsDir))
                    {
                        string ffmpegPath = Path.Combine(depsDir, "ffmpeg");
                        if (File.Exists(ffmpegPath))
                        {
                            FFMPEG.Path_FFMPEG = ffmpegPath;
                            Console.WriteLine($"Found ffmpeg at: {ffmpegPath}");
                        }
                    }

                    // 如果仍然找不到，尝试使用系统ffmpeg
                    if (string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG))
                    {
                        FFMPEG.Path_FFMPEG = "/usr/local/bin/ffmpeg";
                        Console.WriteLine("Using system ffmpeg at: /usr/local/bin/ffmpeg");
                    }
                }

                if (string.IsNullOrWhiteSpace(FFMPEG.Path_FFMPEG) || !File.Exists(FFMPEG.Path_FFMPEG))
                {
                    Console.WriteLine("找不到ffmpeg，无法提取视频预览图像");
                    return string.Empty;
                }

                // 创建临时文件路径
                string tempDir = Path.Combine(Path.GetTempPath(), "yt-dlp-gui");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                string previewImagePath = Path.Combine(tempDir, $"{Path.GetFileNameWithoutExtension(videoFilePath)}_preview.jpg");

                // 使用ffmpeg提取视频的第一帧
                var psi = new ProcessStartInfo
                {
                    FileName = FFMPEG.Path_FFMPEG,
                    Arguments = $"-i \"{videoFilePath}\" -ss 00:00:01 -vframes 1 \"{previewImagePath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                proc.WaitForExit();

                if (proc.ExitCode == 0 && File.Exists(previewImagePath))
                {
                    Console.WriteLine($"成功提取视频预览图像: {previewImagePath}");
                    return previewImagePath;
                }
                else
                {
                    Console.WriteLine("提取视频预览图像失败");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"提取视频预览图像时出错: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 使用系统默认应用打开视频文件
        /// </summary>
        /// <param name="videoFilePath">视频文件路径</param>
        private void OpenVideoFile(string videoFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(videoFilePath) || !File.Exists(videoFilePath))
                {
                    Console.WriteLine("视频文件不存在或路径为空");
                    return;
                }

                Console.WriteLine($"打开视频文件: {videoFilePath}");

                // 使用系统默认应用打开视频文件
                var psi = new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{videoFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开视频文件时出错: {ex.Message}");
            }
        }

        private async Task TestDownloadAsync()
        {
            var urlBox = this.FindControl<TextBox>("UrlTextBox");
            if (urlBox == null || string.IsNullOrWhiteSpace(urlBox.Text))
            {
                Console.WriteLine("Please enter a video URL");
                return;
            }

            string url = urlBox.Text.Trim();
            Console.WriteLine($"测试下载: {url}");

            try
            {
                // 构建测试命令
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--simulate --dump-json \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                // 添加代理设置
                if (!string.IsNullOrWhiteSpace(Data.Proxy))
                {
                    psi.Arguments += $" --proxy \"{Data.Proxy}\"";
                }

                using var proc = Process.Start(psi);
                Console.WriteLine("测试中...");
                
                string output = await proc.StandardOutput.ReadToEndAsync();
                string error = await proc.StandardError.ReadToEndAsync();
                
                await proc.WaitForExitAsync();
                
                if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine("测试成功，视频可正常解析");
                    
                    var infoBox = this.FindControl<TextBox>("VideoInfoBox");
                    if (infoBox != null)
                    {
                        infoBox.Text += "\n\n测试结果: 成功 ✓ 可正常下载";
                    }
                }
                else
                {
                    Console.WriteLine($"测试失败: {error}");
                    
                    var infoBox = this.FindControl<TextBox>("VideoInfoBox");
                    if (infoBox != null)
                    {
                        infoBox.Text += $"\n\n测试结果: 失败 ✗\n{error}";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试过程中发生错误: {ex.Message}");
                
                var infoBox = this.FindControl<TextBox>("VideoInfoBox");
                if (infoBox != null)
                {
                    infoBox.Text += $"\n\n测试结果: 错误 ✗\n{ex.Message}";
                }
            }
        }

        private void Button_OpenFolder(object sender, RoutedEventArgs e)
        {
            // 打开下载文件夹
            if (!string.IsNullOrWhiteSpace(Data.TargetPath) && System.IO.Directory.Exists(Data.TargetPath))
            {
                Console.WriteLine($"打开文件夹: {Data.TargetPath}");
                Libs.Util.OpenFolder(Data.TargetPath);
            }
            else
            {
                Console.WriteLine("目标文件夹不存在");
            }
        }

        // 更新目标文件名显示
        private void UpdateTargetFileName(string targetPath, string filenameTemplate, string extension)
        {
            try
            {
                var targetFileBox = this.FindControl<TextBox>("TargetFileNameBox");
                if (targetFileBox != null)
                {
                    // 替换模板中的基本变量
                    string filename = filenameTemplate;
                    if (Data.Video != null)
                    {
                        filename = filename.Replace("%(title)s", Data.Video.title ?? "video")
                                         .Replace("%(id)s", Data.Video.id ?? "id")
                                         .Replace("%(ext)s", extension)
                                         .Replace("%(format_id)s", "format");
                    }
                    else
                    {
                        filename = filename.Replace("%(title)s", "video")
                                         .Replace("%(id)s", "id")
                                         .Replace("%(ext)s", extension)
                                         .Replace("%(format_id)s", "format");
                    }
                    
                    // 设置显示路径
                    string displayPath = Path.Combine(targetPath, filename);
                    targetFileBox.Text = displayPath;
                    
                    // 保存到数据模型
                    Data.TargetName = filename;
                    
                    Console.WriteLine($"更新目标文件名: {displayPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新目标文件名时出错: {ex.Message}");
            }
        }

        // 自定义证书验证回调函数
        private bool CertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            // 获取请求的主机名
            string? hostname = null;
            if (sender is System.Net.HttpWebRequest request)
            {
                hostname = request.RequestUri.Host;
            }
            else if (sender is System.Net.Http.HttpRequestMessage message)
            {
                hostname = message.RequestUri?.Host;
            }

            // 特殊域名处理 - 为特定的内容网站提供额外的宽松验证
            bool isSpecialDomain = false;
            if (!string.IsNullOrEmpty(hostname))
            {
                // 检查是否为特殊域名
                isSpecialDomain = hostname.Contains("pornhub") ||
                                          hostname.Contains("phncdn") || 
                                          hostname.EndsWith(".ph") ||
                                          hostname.Contains("youjizz") ||
                                  hostname.Contains("yjizz") ||
                                  hostname.Contains("xvideos-cdn") ||
                                  hostname.Contains("xvideos") ||
                                  hostname.Contains("xnxx") ||
                                  hostname.Contains("xnxx-cdn") ||
                                  hostname.Contains("xhamster") ||
                                  hostname.Contains("xhcdn");

                // 为所有特殊域名提供统一的宽松证书验证
                if (isSpecialDomain)
                {
                    string domainType = "";

                    if (hostname.Contains("pornhub") || hostname.Contains("phncdn") || hostname.EndsWith(".ph"))
                    {
                        domainType = "PornHub相关";
                    }
                    else if (hostname.Contains("youjizz") || hostname.Contains("yjizz"))
                    {
                        domainType = "YouJizz相关";
                    }
                    else if (hostname.Contains("xvideos") || hostname.Contains("xvideos-cdn"))
                    {
                        domainType = "XVideos相关";
                    }
                    else if (hostname.Contains("xnxx") || hostname.Contains("xnxx-cdn"))
                    {
                        domainType = "XNXX相关";
                    }
                    else if (hostname.Contains("xhamster") || hostname.Contains("xhcdn"))
                    {
                        domainType = "xHamster相关";
                    }

                    Console.WriteLine($"检测到{domainType}域名: {hostname}，将完全放宽证书验证限制");
                    return true; // 为所有特殊域名直接接受所有证书，不进行任何验证
                }
            }
            
            // 如果没有错误，返回 true
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }
            
            // 记录证书错误
            Console.WriteLine($"证书验证错误: {sslPolicyErrors}，主机名: {hostname}");
            
            if (certificate != null)
            {
                Console.WriteLine($"证书信息: {certificate.Subject}");
                Console.WriteLine($"证书颁发者: {certificate.Issuer}");
                Console.WriteLine($"证书有效期: {certificate.GetEffectiveDateString()} - {certificate.GetExpirationDateString()}");
                
                try 
                {
                    // 尝试获取更多证书详细信息
                    X509Certificate2 cert2 = new X509Certificate2(certificate);
                    Console.WriteLine($"证书序列号: {cert2.SerialNumber}");
                    Console.WriteLine($"签名算法: {cert2.SignatureAlgorithm.FriendlyName}");
                    Console.WriteLine($"证书版本: {cert2.Version}");
                    
                    // 检查证书是否在有效期内
                    bool isTimeValid = DateTime.Now > cert2.NotBefore && DateTime.Now < cert2.NotAfter;
                    Console.WriteLine($"证书时间有效性: {isTimeValid}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"获取证书扩展信息时出错: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("证书对象为空，无法获取详细信息");
            }

            // 分别处理各种错误类型
            // 1. 证书名称不匹配
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                Console.WriteLine("证书名称不匹配错误: 这可能是因为证书上的域名与请求的域名不匹配");
                // 对于内容网站的缩略图，我们可以接受这类错误
            }
            
            // 2. 证书链错误
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
            {
                Console.WriteLine("证书链错误: 这可能是由于缺少根证书或中间证书");
                
                if (chain != null)
                {
                    Console.WriteLine($"证书链状态: {(chain.ChainStatus.Length == 0 ? "有效" : "无效")}");
                    foreach (var status in chain.ChainStatus)
                    {
                        Console.WriteLine($"Chain错误详情: {status.Status} - {status.StatusInformation}");
                    }
                    
                    // 打印证书链中的每个证书信息
                    for (int i = 0; i < chain.ChainElements.Count; i++)
                    {
                        Console.WriteLine($"证书链元素 #{i}: {chain.ChainElements[i].Certificate.Subject}");
                    }
                }
                else
                {
                    Console.WriteLine("证书链对象为空，无法检查详细信息");
                }
            }
            
            // 3. 证书本身无效
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
            {
                Console.WriteLine("远程证书不可用: 服务器未提供有效证书");
            }
            
            // 为了保证缩略图能正常加载，对于非敏感操作我们接受所有证书
            // 注意: 在生产环境中，对于敏感操作应当有更严格的验证
            Console.WriteLine("由于这只是加载缩略图的非敏感操作，将继续处理");
            return true;
        }
    }
}
