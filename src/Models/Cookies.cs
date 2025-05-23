using System;

namespace yt_dlp_gui_mac.Models
{
    public enum UseCookie
    {
        WhenNeeded,
        Never,
        Always,
        Ask
    }

    public enum CookieType
    {
        Chrome,
        Edge,
        Firefox,
        Opera,
        Safari,
        Chromium,
        Chrome_Beta
    }
}
