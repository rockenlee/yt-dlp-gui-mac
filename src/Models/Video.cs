using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace yt_dlp_gui_mac.Models
{
    public class Video
    {
        public string id { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string thumbnail { get; set; } = string.Empty;
        public string webpage_url { get; set; } = string.Empty;
        public string _filename { get; set; } = string.Empty;
        public string upload_date { get; set; } = string.Empty;
        public string uploader { get; set; } = string.Empty;
        public string uploader_id { get; set; } = string.Empty;
        public string uploader_url { get; set; } = string.Empty;
        public string channel { get; set; } = string.Empty;
        public string channel_id { get; set; } = string.Empty;
        public string channel_url { get; set; } = string.Empty;
        public double duration { get; set; } = 0;
        public int view_count { get; set; } = 0;
        public int like_count { get; set; } = 0;
        public int dislike_count { get; set; } = 0;
        public int comment_count { get; set; } = 0;
        public string age_limit { get; set; } = string.Empty;
        public string live_status { get; set; } = string.Empty;
        public bool is_live { get; set; } = false;
        public string release_date { get; set; } = string.Empty;
        public List<string> tags { get; set; } = new();
        public List<string> categories { get; set; } = new();
        public List<VideoFormat> formats { get; set; } = new();
        public List<VideoThumbnail> thumbnails { get; set; } = new();
    }

    public class VideoFormat
    {
        public string? format_id { get; set; }
        public string? format_note { get; set; }
        public string? ext { get; set; }
        public string? acodec { get; set; }
        public string? vcodec { get; set; }
        public string? url { get; set; }
        public int? width { get; set; }
        public int? height { get; set; }
        public double? fps { get; set; }
        public double? abr { get; set; }
        public double? vbr { get; set; }
        public double? tbr { get; set; }
        public string? format { get; set; }
    }

    public class VideoThumbnail
    {
        public string? id { get; set; }
        public string? url { get; set; }
        public int? width { get; set; }
        public int? height { get; set; }
        public string? resolution { get; set; }
    }
}
