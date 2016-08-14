using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using RT.Util.ExtensionMethods;

namespace RandomWallpaper
{
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

        public static void Set(string path)
        {
            var screen = Screen.AllScreens.MaxElement(s => s.Bounds.Width * s.Bounds.Height).Bounds;
            var cachedPath = Path.Combine(Path.GetDirectoryName(path), "RandomWallpaper-" + screen.Width + "x" + screen.Height, Path.GetFileName(path));

            if (File.Exists(cachedPath))
            {
                var cached = new FileInfo(cachedPath);
                var real = new FileInfo(path);
                if (cached.LastWriteTimeUtc != real.LastWriteTimeUtc || cached.Length != real.Length)
                    File.Delete(cachedPath);
            }

            if (!File.Exists(cachedPath))
            {
                var dirPath = Path.GetDirectoryName(cachedPath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                    File.SetAttributes(dirPath, File.GetAttributes(dirPath) | FileAttributes.Hidden);
                }

                bool justCopy = false;
                using (var orig = new Bitmap(path))
                {
                    if (orig.Width == screen.Width && orig.Height == screen.Height)
                        justCopy = true;
                    else
                    {
                        var result = new Bitmap(screen.Width, screen.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                        using (var dc = Graphics.FromImage(result))
                        {
                            dc.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            dc.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            var imageAttrs = new ImageAttributes();
                            imageAttrs.SetWrapMode(WrapMode.TileFlipXY);
                            if (orig.Width / (double) orig.Height > screen.Width / (double) screen.Height) // either code path is fine if the original is the correct aspect ratio but just too large
                            {
                                // The original is too wide
                                float sourceWidth = screen.Width * orig.Height / (float) screen.Height;
                                dc.DrawImage(orig, new Rectangle(0, 0, screen.Width, screen.Height), (orig.Width - sourceWidth) / 2, 0, sourceWidth, orig.Height, GraphicsUnit.Pixel, imageAttrs);
                            }
                            else
                            {
                                // The original is too tall
                                float sourceHeight = screen.Height * orig.Width / (float) screen.Width;
                                dc.DrawImage(orig, new Rectangle(0, 0, screen.Width, screen.Height), 0, (orig.Height - sourceHeight) / 2, orig.Width, sourceHeight, GraphicsUnit.Pixel, imageAttrs);
                            }
                        }
                        var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        var encParams = new EncoderParameters() { Param = new[] { new EncoderParameter(Encoder.Quality, 92L) } };
                        result.Save(cachedPath, encoder, encParams);
                    }
                }
                if (justCopy)
                    File.Copy(path, cachedPath);
            }

            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Internet Explorer\Desktop\General", true);
            key.SetValue("WallpaperSource", path);
            key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            key.SetValue("WallpaperStyle", "0");
            key.SetValue("TileWallpaper", "0");
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, cachedPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }

        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
    }
}
