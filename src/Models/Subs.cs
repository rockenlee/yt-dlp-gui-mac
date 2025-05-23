namespace yt_dlp_gui_mac.Models
{
    public class Subs
    {
        public string name { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
        public string ext { get; set; } = string.Empty;
        
        public override string ToString()
        {
            return $"{name} [{ext}]";
        }
    }
}
