namespace yt_dlp_gui_mac.Models
{
    public class Lang
    {
        public string AppName { get; set; } = "yt-dlp GUI";
        public MainLang Main { get; set; } = new();

        public class MainLang
        {
            public string Main { get; set; } = "Main";
            public string Settings { get; set; } = "Settings";
            public string About { get; set; } = "About";
            public string URL { get; set; } = "URL";
            public string Analyze { get; set; } = "Analyze";
            public string Download { get; set; } = "Download";
            public string Cancel { get; set; } = "Cancel";
            public string Browse { get; set; } = "Browse";
            public string Format { get; set; } = "Format";
            public string SaveTo { get; set; } = "Save to";
            public string AlwaysOnTop { get; set; } = "Always on top";
            public string MonitorClipboard { get; set; } = "Monitor clipboard";
            public string RememberPosition { get; set; } = "Remember window position";
            public string RememberSize { get; set; } = "Remember window size";
            public string Dependencies { get; set; } = "Dependencies";
            public string TemporaryTarget { get; set; } = "Target directory";
            public string TemporaryLocale { get; set; } = "Local directory";
            public string TemporarySystem { get; set; } = "System temporary directory";
            public string TemporaryBrowse { get; set; } = "Browse...";
        }
    }
}
