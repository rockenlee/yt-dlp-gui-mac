using System.Collections.Generic;

namespace yt_dlp_gui_mac.Models
{
    public class Config
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, string> Options { get; set; } = new();
        
        public override string ToString()
        {
            return Name;
        }
    }
}
