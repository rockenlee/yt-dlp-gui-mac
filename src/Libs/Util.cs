using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace yt_dlp_gui_mac.Libs
{
    public static partial class Util
    {
        // 属性复制
        public static void PropertyCopy<T, U>(T source, U target)
            where T : class
            where U : class
        {
            var sourceProps = typeof(T).GetProperties().Where(x => x.CanRead).ToList();
            var targetProps = typeof(U).GetProperties().Where(x => x.CanWrite).ToList();
            
            foreach (var sourceProp in sourceProps)
            {
                var targetProp = targetProps.FirstOrDefault(x => x.Name == sourceProp.Name && x.PropertyType == sourceProp.PropertyType);
                if (targetProp != null)
                {
                    var value = sourceProp.GetValue(source, null);
                    targetProp.SetValue(target, value, null);
                }
            }
        }
        
        // URL验证
        public static bool UrlVaild(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            
            try
            {
                var uri = new Uri(url);
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            }
            catch
            {
                return false;
            }
        }
        
        // 打开文件夹
        public static async Task OpenFolder(string path)
        {
            await Task.Run(() =>
            {
                if (File.Exists(path))
                {
                    Process.Start("open", $"-R \"{path}\"");
                }
                else if (Directory.Exists(path))
                {
                    Process.Start("open", $"\"{path}\"");
                }
            });
        }
        
        // 正则表达式组提取
        public static Dictionary<string, string> GetGroup(Regex r, string input)
        {
            var m = r.Match(input);
            if (m.Success)
            {
                var groupData = r.GetGroupNames()
                    .Where(x => !string.IsNullOrWhiteSpace(m.Groups[x]?.Value))
                    .ToDictionary(x => x.ToLower(), x => m.Groups[x]);
                var group = groupData.ToDictionary(x => x.Key, x => x.Value.Value.Trim());
                return group;
            }
            return new Dictionary<string, string>();
        }
        
        // 播放通知声音
        public static void NotifySound(string path = "")
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                // 使用系统默认通知声音
                Process.Start("afplay", "/System/Library/Sounds/Ping.aiff");
            }
            else
            {
                Process.Start("afplay", $"\"{path}\"");
            }
        }
    }
}
