namespace yt_dlp_gui_mac.Models
{
    public class Chapters
    {
        public string title { get; set; } = string.Empty;
        public double start_time { get; set; } = 0;
        public double end_time { get; set; } = 0;
        
        public override string ToString()
        {
            return $"{FormatTime(start_time)} - {FormatTime(end_time)}: {title}";
        }
        
        private string FormatTime(double seconds)
        {
            var timeSpan = System.TimeSpan.FromSeconds(seconds);
            return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
    }
}
