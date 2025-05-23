using System;
using System.Collections.Generic;

namespace yt_dlp_gui_mac.Models
{
    public class GitRelease
    {
        public string tag_name { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string body { get; set; } = string.Empty;
        public DateTime published_at { get; set; }
        public List<GitAsset> assets { get; set; } = new();
    }
    
    public class GitAsset
    {
        public string name { get; set; } = string.Empty;
        public string browser_download_url { get; set; } = string.Empty;
        public int download_count { get; set; } = 0;
        public long size { get; set; } = 0;
    }
}
