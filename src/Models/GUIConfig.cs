namespace yt_dlp_gui_mac.Models
{
    public class GUIConfig
    {
        public bool AlwaysOnTop { get; set; } = false;
        public bool MonitorClipboard { get; set; } = true;
        public bool RememberWindowStatePosition { get; set; } = true;
        public bool RememberWindowStateSize { get; set; } = true;
        public double Top { get; set; } = 0;
        public double Left { get; set; } = 0;
        public double Width { get; set; } = 600;
        public double Height { get; set; } = 380;
        public int Scale { get; set; } = 100;
        public string PathYTDLP { get; set; } = string.Empty;
        public string PathFFMPEG { get; set; } = string.Empty;
        public string PathAria2 { get; set; } = string.Empty;
        public string PathTEMP { get; set; } = string.Empty;
        public string PathNotify { get; set; } = string.Empty;
        public string LastVersion { get; set; } = string.Empty;
        public string LastCheckUpdate { get; set; } = string.Empty;

        public void Load(string path)
        {
            if (System.IO.File.Exists(path))
            {
                try
                {
                    var yaml = System.IO.File.ReadAllText(path);
                    var deserializer = new YamlDotNet.Serialization.Deserializer();
                    var config = deserializer.Deserialize<GUIConfig>(yaml);

                    if (config != null)
                    {
                        Libs.Util.PropertyCopy(config, this);
                    }
                }
                catch
                {
                    // Failed to load, using default values
                }
            }
        }

        public void Save(string path)
        {
            try
            {
                var serializer = new YamlDotNet.Serialization.Serializer();
                var yaml = serializer.Serialize(this);
                System.IO.File.WriteAllText(path, yaml);
            }
            catch
            {
                // Failed to save
            }
        }
    }
}
