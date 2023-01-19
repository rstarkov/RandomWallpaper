using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace RandomWallpaper;

static class Wallpaper
{
    public static string Get()
    {
        string source = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Internet Explorer\Desktop\General", false).GetValue("WallpaperSource", null) as string;
        string actual = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false).GetValue("Wallpaper", null) as string;
        if (source == null)
            return actual;
        if (actual.Contains(@"\RandomWallpaper-"))
            return source;
        if (actual.EndsWith(@"\Themes\TranscodedWallpaper.jpg", StringComparison.OrdinalIgnoreCase))
            return source;
        return actual;
    }

    public static void SetWallpaper(string path)
    {
        var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Internet Explorer\Desktop\General", true);
        key.SetValue("WallpaperSource", path);
        key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
        key.SetValue("WallpaperStyle", "10"); // 0 = center, 10 = fill
        key.SetValue("TileWallpaper", "0");
        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
    }

    public static void SetLockscreen(string path)
    {
        try
        {
            var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP", true);
            if (key == null)
                Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP", true);
            key.SetValue("LockScreenImagePath", path);
        }
        catch (SecurityException)
        {
            throw new TellUserException(Program.Colorize("{red}Error:{} updating the lock screen image requires elevation."), Program.ErrorUser);
        }
    }

    const int SPI_SETDESKWALLPAPER = 20;
    const int SPIF_UPDATEINIFILE = 0x01;
    const int SPIF_SENDWININICHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
}
