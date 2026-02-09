using System;
using System.Runtime.InteropServices;

namespace WinPanel.Services
{
    /// <summary>
    /// Controls screen brightness via gamma ramp adjustment.
    /// Works on ALL monitors regardless of DDC/CI support.
    /// </summary>
    public class GammaService
    {
        [DllImport("gdi32.dll")]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("gdi32.dll")]
        private static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct RAMP
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;
        }

        private int _currentBrightness = 100;

        public bool IsAvailable()
        {
            IntPtr hDC = GetDC(IntPtr.Zero);
            if (hDC == IntPtr.Zero) return false;
            
            RAMP ramp = new RAMP
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256]
            };
            
            bool result = GetDeviceGammaRamp(hDC, ref ramp);
            ReleaseDC(IntPtr.Zero, hDC);
            return result;
        }

        public int GetBrightness()
        {
            return _currentBrightness;
        }

        public void SetBrightness(int level)
        {
            if (level < 10) level = 10;  // Minimum 10% to avoid black screen
            if (level > 100) level = 100;

            _currentBrightness = level;

            IntPtr hDC = GetDC(IntPtr.Zero);
            if (hDC == IntPtr.Zero) return;

            RAMP ramp = new RAMP
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256]
            };

            double brightness = level / 100.0;

            for (int i = 0; i < 256; i++)
            {
                ushort value = (ushort)(i * 255 * brightness);
                ramp.Red[i] = value;
                ramp.Green[i] = value;
                ramp.Blue[i] = value;
            }

            SetDeviceGammaRamp(hDC, ref ramp);
            ReleaseDC(IntPtr.Zero, hDC);
        }

        public void Reset()
        {
            SetBrightness(100);
        }
    }
}
