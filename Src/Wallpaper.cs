using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace RandomWallpaper
{
    static class Wallpaper
    {
        public static string Get()
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false);
            var wallpaper = key.GetValue("Wallpaper", null) as string;
            if (wallpaper != null && !wallpaper.EndsWith(@"\Themes\TranscodedWallpaper.jpg", StringComparison.OrdinalIgnoreCase))
                return wallpaper;
            key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Internet Explorer\Desktop\General", false);
            return key.GetValue("WallpaperSource", null) as string;
        }

        public static void Set(string path)
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            key.SetValue("WallpaperStyle", 10); // Fill
            key.SetValue("TileWallpaper", 0);
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }

        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
    }
}
