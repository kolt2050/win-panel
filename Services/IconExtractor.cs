using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinPanel.Services
{
    public static class IconExtractor
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHGetFileInfo(string pszPath, uint dwFileAttributes, 
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;

        public static ImageSource? GetIcon(string path)
        {
            try
            {
                var ext = Path.GetExtension(path).ToLower();
                
                // For .url files, try to get associated icon
                if (ext == ".url")
                {
                    return GetUrlIcon(path);
                }
                
                // Extract icon using Shell API
                var icon = ExtractIconFromFile(path);
                
                // Fallback to System.Drawing if Shell API fails
                if (icon == null && File.Exists(path))
                {
                    try
                    {
                        using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                        if (sysIcon != null)
                        {
                            return Imaging.CreateBitmapSourceFromHIcon(
                                sysIcon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                    }
                    catch { }
                }

                return icon;
            }
            catch
            {
                return null;
            }
        }



        private static ImageSource? ExtractIconFromFile(string filePath)
        {
            try
            {
                var shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_LARGEICON;
                
                SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                
                if (shinfo.hIcon != IntPtr.Zero)
                {
                    var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    
                    DestroyIcon(shinfo.hIcon);
                    return imageSource;
                }
            }
            catch
            {
            }
            return null;
        }

        private static ImageSource? GetUrlIcon(string urlPath)
        {
            try
            {
                // Try to read icon from .url file
                var lines = File.ReadAllLines(urlPath);
                string? iconFile = null;
                
                foreach (var line in lines)
                {
                    if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        iconFile = line.Substring(9);
                        break;
                    }
                }
                
                if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
                {
                    return ExtractIconFromFile(iconFile);
                }
                
                // Use shell to get default browser icon
                return ExtractIconFromFile(urlPath);
            }
            catch
            {
                return null;
            }
        }
    }
}
