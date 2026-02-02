using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace WinPanel.Services
{
    public static class SystemService
    {
        private const string AppName = "WinPanel";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetAutostart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (string.IsNullOrEmpty(exePath)) return;
                    
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                    {
                        key.DeleteValue(AppName);
                    }
                }
            }
            catch (Exception)
            {
                // Handle permissions issues silently or log
            }
        }

        public static bool IsAutostartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        public static void FullUninstall()
        {
            try
            {
                // 1. Remove Autostart
                SetAutostart(false);

                // 2. Remove Config Directory
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var configDir = Path.Combine(appData, "WinPanel");
                if (Directory.Exists(configDir))
                {
                    Directory.Delete(configDir, true);
                }

                // 3. Self Delete via CMD
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C timeout 2 & del /f /q \"{exePath}\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = true
                    });
                }

                // 4. Close App
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при удалении: {ex.Message}");
            }
        }
    }
}
