using System;
using System.Collections.Generic;

namespace yt_dlp_gui_mac.Models
{
    public enum FormatType
    {
        unknown, video, audio, package
    }

    public class Format
    {
        public string format_id { get; set; } = string.Empty;
        public string format_note { get; set; } = string.Empty;
        public string ext { get; set; } = string.Empty;
        public string acodec { get; set; } = string.Empty;
        public string vcodec { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
        public int? width { get; set; } = 0;
        public int? height { get; set; } = 0;
        public double? fps { get; set; } = 0;
        public double? abr { get; set; } = 0;
        public double? vbr { get; set; } = 0;
        public double? tbr { get; set; } = 0;
        public string format { get; set; } = string.Empty;
        public string resolution { get; set; } = string.Empty;
        public string dynamic_range { get; set; } = string.Empty;
        public FormatType type { get; set; } = FormatType.unknown;

        public override string ToString()
        {
            return $"{format_id} - {format_note} ({width ?? 0}x{height ?? 0}) [{ext}]";
        }
    }

    public class ComparerVideo : IComparer<Format>
    {
        public static ComparerVideo Comparer { get; } = new ComparerVideo();

        public int Compare(Format? x, Format? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // 比较分辨率
            var resX = (x.width ?? 0) * (x.height ?? 0);
            var resY = (y.width ?? 0) * (y.height ?? 0);

            if (resX != resY)
                return resX.CompareTo(resY);

            // 比较帧率
            double xfps = x.fps ?? 0;
            double yfps = y.fps ?? 0;
            if (xfps != yfps)
                return xfps.CompareTo(yfps);

            // 比较视频比特率
            return (x.vbr ?? 0).CompareTo(y.vbr ?? 0);
        }
    }

    public class ComparerAudio : IComparer<Format>
    {
        public static ComparerAudio Comparer { get; } = new ComparerAudio();

        public int Compare(Format? x, Format? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // 比较音频比特率
            return (x.abr ?? 0).CompareTo(y.abr ?? 0);
        }
    }
}
