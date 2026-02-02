using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinPanel.Services
{
    public static class WindowEffect
    {
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        public static void EnableBlur(Window window)
        {
            try
            {
                var windowHelper = new WindowInteropHelper(window);
                var accent = new AccentPolicy();
                
                // For AllowsTransparency="True", reliable blur is tricky.
                // We use standard BLURBEHIND which works on some configurations
                // Use 0x01000000 (ABGR) to make it barely visible but active
                accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;
                accent.GradientColor = 0; 

                var accentStructSize = Marshal.SizeOf(accent);
                var accentPtr = Marshal.AllocHGlobal(accentStructSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData();
                data.Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY;
                data.SizeOfData = accentStructSize;
                data.Data = accentPtr;

                SetWindowCompositionAttribute(windowHelper.Handle, ref data);

                Marshal.FreeHGlobal(accentPtr);
            }
            catch (Exception)
            {
            }
        }
    }
}
