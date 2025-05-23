using System;
using System.IO;
using YamlDotNet.Serialization;

namespace yt_dlp_gui_mac.Libs
{
    public static class Yaml
    {
        public static T Open<T>(string path) where T : new()
        {
            if (File.Exists(path))
            {
                try
                {
                    var yaml = File.ReadAllText(path);
                    var deserializer = new DeserializerBuilder().Build();
                    return deserializer.Deserialize<T>(yaml);
                }
                catch (Exception)
                {
                    return new T();
                }
            }
            return new T();
        }
        
        public static void Save<T>(string path, T obj)
        {
            try
            {
                var serializer = new SerializerBuilder().Build();
                var yaml = serializer.Serialize(obj);
                File.WriteAllText(path, yaml);
            }
            catch (Exception)
            {
                // 保存失败
            }
        }
    }
}
