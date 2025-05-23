using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using yt_dlp_gui_mac.Models;

namespace yt_dlp_gui_mac.Models
{
    public class ViewData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _isAnalyze;
        public bool IsAnalyze
        {
            get => _isAnalyze;
            set
            {
                if (_isAnalyze != value)
                {
                    _isAnalyze = value;
                    OnPropertyChanged(nameof(IsAnalyze));
                }
            }
        }

        private bool _isAnalyzed;
        public bool IsAnalyzed
        {
            get => _isAnalyzed;
            set
            {
                if (_isAnalyzed != value)
                {
                    _isAnalyzed = value;
                    OnPropertyChanged(nameof(IsAnalyzed));
                }
            }
        }

        private bool _isAnalyzing;
        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            set
            {
                if (_isAnalyzing != value)
                {
                    _isAnalyzing = value;
                    OnPropertyChanged(nameof(IsAnalyzing));

                    // 当分析状态改变时，更新UI状态
                    if (value)
                    {
                        // 开始分析时，禁用某些功能
                        IsAnalyze = true;
                    }
                    else
                    {
                        // 分析完成时，恢复功能
                        IsAnalyze = false;
                    }
                }
            }
        }

        // Basic properties
        public string Url { get; set; } = string.Empty;

        private string _targetPath = string.Empty;
        public string TargetPath
        {
            get => _targetPath;
            set
            {
                if (_targetPath != value)
                {
                    _targetPath = value;
                    OnPropertyChanged(nameof(TargetPath));
                }
            }
        }

        public string TargetName { get; set; } = string.Empty;
        public string TargetFile => System.IO.Path.Combine(TargetPath, TargetName);

        // Video information
        public Video? Video { get; set; } = new();
        public string Thumbnail { get; set; } = string.Empty;

        // Formats
        public ObservableCollection<Format> Formats { get; set; } = new();
        public IEnumerable<Format> FormatsView => Formats.OrderBy(x => x.width * x.height);
        public IEnumerable<Format> FormatsVideo => Formats.Where(x => x.type == FormatType.package || x.type == FormatType.video).OrderBy(x => x.width * x.height);
        public IEnumerable<Format> FormatsAudio => Formats.Where(x => x.type == FormatType.package || x.type == FormatType.audio).OrderBy(x => x.abr);

        // Thumbnails
        public ObservableCollection<Thumb> Thumbnails { get; set; } = new();
        public IEnumerable<Thumb> ThumbnailsView => Thumbnails;

        // Subtitles
        public ObservableCollection<Subs> Subtitles { get; set; } = new();
        public IEnumerable<Subs> SubtitlesView => Subtitles;

        // Chapters
        public ObservableCollection<Chapters> Chapters { get; set; } = new();
        public IEnumerable<Chapters> ChaptersView => Chapters;

        // Configurations
        public ObservableCollection<Config> Configs { get; set; } = new();
        public IEnumerable<Config> ConfigsView => Configs;
        public Config selectedConfig { get; set; } = new();

        // Download options
        public bool RemuxVideo { get; set; } = false;
        public bool UseFormat { get; set; } = true;
        public bool UseOutput { get; set; } = true;

        // Download status
        private bool _isDownload = false;
        public bool IsDownload
        {
            get => _isDownload;
            set
            {
                if (_isDownload != value)
                {
                    _isDownload = value;
                    OnPropertyChanged(nameof(IsDownload));
                }
            }
        }

        public bool IsAbouted { get; set; } = false;

        // Download progress
        private DownloadStatus _dnStatus_Video = new DownloadStatus();
        public DownloadStatus DNStatus_Video
        {
            get => _dnStatus_Video;
            set
            {
                if (_dnStatus_Video != value)
                {
                    _dnStatus_Video = value;
                    OnPropertyChanged(nameof(DNStatus_Video));
                }
            }
        }

        private DownloadStatus _dnStatus_Audio = new DownloadStatus();
        public DownloadStatus DNStatus_Audio
        {
            get => _dnStatus_Audio;
            set
            {
                if (_dnStatus_Audio != value)
                {
                    _dnStatus_Audio = value;
                    OnPropertyChanged(nameof(DNStatus_Audio));
                }
            }
        }
        public Dictionary<string, string> DNStatus_Infos { get; set; } = new();

        // Settings
        public bool AlwaysOnTop { get; set; } = false;
        public bool MonitorClipboard { get; set; } = true;
        public bool RememberWindowStatePosition { get; set; } = true;
        public bool RememberWindowStateSize { get; set; } = true;
        public double Top { get; set; } = 0;
        public double Left { get; set; } = 0;
        public double Width { get; set; } = 600;
        public double Height { get; set; } = 380;
        public int Scale { get; set; } = 100;

        // Dependency paths
        public string PathYTDLP { get; set; } = string.Empty;
        public string PathFFMPEG { get; set; } = string.Empty;
        public string PathAria2 { get; set; } = string.Empty;

        // Temporary directory
        public string PathTEMP { get; set; } = string.Empty;

        // Notification sound
        public string PathNotify { get; set; } = string.Empty;

        // Update information
        public bool NewVersion { get; set; } = false;
        public string LastVersion { get; set; } = string.Empty;
        public string LastCheckUpdate { get; set; } = string.Empty;
        public List<GitRelease> ReleaseData { get; set; } = new();

        // Configuration
        public GUIConfig GUIConfig { get; set; } = new();
        public bool AutoSaveConfig { get; set; } = false;

        // Clipboard
        public string ClipboardText { get; set; } = string.Empty;

        // Select best format
        public void SelectFormatBest()
        {
            // Implement logic to select the best format
            if (Formats.Count == 0)
                return;

            // Try to find the best video+audio package
            var bestPackage = Formats
                .Where(f => f.type == FormatType.package)
                .OrderByDescending(f => f.width * f.height)
                .ThenByDescending(f => f.fps)
                .ThenByDescending(f => f.vbr)
                .FirstOrDefault();

            if (bestPackage != null)
            {
                // Found the best package, set it as selected
                selectedFormat = bestPackage;
                return;
            }

            // No package found, try to find the best video and best audio
            var bestVideo = Formats
                .Where(f => f.type == FormatType.video)
                .OrderByDescending(f => f.width * f.height)
                .ThenByDescending(f => f.fps)
                .ThenByDescending(f => f.vbr)
                .FirstOrDefault();

            var bestAudio = Formats
                .Where(f => f.type == FormatType.audio)
                .OrderByDescending(f => f.abr)
                .FirstOrDefault();

            if (bestVideo != null)
            {
                // Found the best video, set it as selected
                selectedFormat = bestVideo;
            }
            else if (bestAudio != null)
            {
                // Only found audio, set it as selected
                selectedFormat = bestAudio;
            }
            else if (Formats.Count > 0)
            {
                // No video or audio found, select the first format
                selectedFormat = Formats[0];
            }
        }

        // Currently selected format
        public Format selectedFormat { get; set; } = new();

        // Subtitle language
        public string SubLang
        {
            get => _subLang;
            set { if (_subLang != value) { _subLang = value; OnPropertyChanged(nameof(SubLang)); } }
        }
        private string _subLang = "en.*,ja.*,zh.*";

        // Filename template
        public string FilenameTemplate
        {
            get => _filenameTemplate;
            set { if (_filenameTemplate != value) { _filenameTemplate = value; OnPropertyChanged(nameof(FilenameTemplate)); } }
        }
        private string _filenameTemplate = "%(title)s.%(ext)s";

        // Open folder after download
        public bool OpenFolderAfterDownload
        {
            get => _openFolderAfterDownload;
            set { if (_openFolderAfterDownload != value) { _openFolderAfterDownload = value; OnPropertyChanged(nameof(OpenFolderAfterDownload)); } }
        }
        private bool _openFolderAfterDownload = true;

        // Auto monitor clipboard
        public bool AutoMonitorClipboard
        {
            get => _autoMonitorClipboard;
            set { if (_autoMonitorClipboard != value) { _autoMonitorClipboard = value; OnPropertyChanged(nameof(AutoMonitorClipboard)); } }
        }
        private bool _autoMonitorClipboard = true;

        // Extra arguments
        public string ExtraArgs
        {
            get => _extraArgs;
            set { if (_extraArgs != value) { _extraArgs = value; OnPropertyChanged(nameof(ExtraArgs)); } }
        }
        private string _extraArgs = "";

        // Proxy settings
        public string Proxy
        {
            get => _proxy;
            set { if (_proxy != value) { _proxy = value; OnPropertyChanged(nameof(Proxy)); } }
        }
        private string _proxy = "";

        // Cookie settings
        public UseCookie UseCookie
        {
            get => _useCookie;
            set { if (_useCookie != value) { _useCookie = value; OnPropertyChanged(nameof(UseCookie)); } }
        }
        private UseCookie _useCookie = UseCookie.WhenNeeded;

        public CookieType CookieType
        {
            get => _cookieType;
            set { if (_cookieType != value) { _cookieType = value; OnPropertyChanged(nameof(CookieType)); } }
        }
        private CookieType _cookieType = CookieType.Chrome;

        public bool NeedCookie
        {
            get => _needCookie;
            set { if (_needCookie != value) { _needCookie = value; OnPropertyChanged(nameof(NeedCookie)); } }
        }
        private bool _needCookie = false;

        // Download thumbnail
        public bool DownloadThumbnail
        {
            get => _downloadThumbnail;
            set { if (_downloadThumbnail != value) { _downloadThumbnail = value; OnPropertyChanged(nameof(DownloadThumbnail)); } }
        }
        private bool _downloadThumbnail = false;

        // Include auto-generated subtitles
        public bool IncludeAutoSubtitles
        {
            get => _includeAutoSubtitles;
            set { if (_includeAutoSubtitles != value) { _includeAutoSubtitles = value; OnPropertyChanged(nameof(IncludeAutoSubtitles)); } }
        }
        private bool _includeAutoSubtitles = true;
    }

    public class DownloadStatus : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private double _percent = 0;
        public double Percent
        {
            get => _percent;
            set
            {
                if (_percent != value)
                {
                    _percent = value;
                    OnPropertyChanged(nameof(Percent));
                }
            }
        }

        private string _speed = string.Empty;
        public string Speed
        {
            get => _speed;
            set
            {
                if (_speed != value)
                {
                    _speed = value;
                    OnPropertyChanged(nameof(Speed));
                    OnPropertyChanged(nameof(HasSpeed));
                }
            }
        }

        public bool HasSpeed => !string.IsNullOrWhiteSpace(_speed);

        private string _eta = string.Empty;
        public string ETA
        {
            get => _eta;
            set
            {
                if (_eta != value)
                {
                    _eta = value;
                    OnPropertyChanged(nameof(ETA));
                    OnPropertyChanged(nameof(HasETA));
                }
            }
        }

        public bool HasETA => !string.IsNullOrWhiteSpace(_eta);

        private string _size = string.Empty;
        public string Size
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    OnPropertyChanged(nameof(Size));
                }
            }
        }
    }
}
