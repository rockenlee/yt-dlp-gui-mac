using System.Diagnostics;
using System.IO;

namespace yt_dlp_gui_mac.Wrappers
{
    public static class FFMPEG
    {
        public static string Path_FFMPEG { get; set; } = string.Empty;
        
        public static void Remux(string input, string output)
        {
            if (string.IsNullOrWhiteSpace(Path_FFMPEG) || !File.Exists(Path_FFMPEG))
            {
                return;
            }
            
            var args = $"-i \"{input}\" -c copy \"{output}\"";
            Exec(args);
        }
        
        public static void Merge(string video, string audio, string output)
        {
            if (string.IsNullOrWhiteSpace(Path_FFMPEG) || !File.Exists(Path_FFMPEG))
            {
                return;
            }
            
            var args = $"-i \"{video}\" -i \"{audio}\" -c copy \"{output}\"";
            Exec(args);
        }
        
        private static void Exec(string args = "")
        {
            var fn = Path_FFMPEG;
            Process p = new Process();
            p.StartInfo.FileName = fn;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.EnableRaisingEvents = true;
            p.OutputDataReceived += (s, e) =>
            {
                Debug.WriteLine(e.Data, "STD");
            };
            p.ErrorDataReceived += (s, e) =>
            {
                Debug.WriteLine(e.Data, "ERR");
            };
            p.Start();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            p.WaitForExit();
        }
    }
}
