using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using yt_dlp_gui_mac.Models;

namespace yt_dlp_gui_mac.Libs
{
    public static class Web
    {
        private static readonly HttpClient client = new HttpClient();
        
        // 检查URL是否可访问
        public static bool Head(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = client.Send(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        
        // 获取最新版本信息
        public static async Task<List<GitRelease>> GetLastTag()
        {
            try
            {
                var response = await client.GetStringAsync("https://api.github.com/repos/ytdl-patched/yt-dlp-gui/releases");
                return JsonConvert.DeserializeObject<List<GitRelease>>(response) ?? new List<GitRelease>();
            }
            catch
            {
                return new List<GitRelease>();
            }
        }
    }
}
