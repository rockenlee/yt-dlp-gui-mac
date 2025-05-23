namespace yt_dlp_gui_mac.Models
{
    public class Thumb
    {
        public string id { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
        public int width { get; set; } = 0;
        public int height { get; set; } = 0;
        public string resolution { get; set; } = string.Empty;
        
        public override string ToString()
        {
            return $"{resolution} ({width}x{height})";
        }
    }
}
