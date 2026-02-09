using System;
using System.IO;
using System.Windows;

namespace WinPanel
{
    public partial class App : Application
    {
        public App()
        {
            // Catch all unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogError("UnhandledException", e.ExceptionObject as Exception);
            };
            
            DispatcherUnhandledException += (s, e) =>
            {
                LogError("DispatcherUnhandledException", e.Exception);
                e.Handled = true;
            };
        }

        private static void LogError(string source, Exception? ex)
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "WinPanel_Error.log");
                var message = $"[{DateTime.Now}] {source}: {ex?.ToString() ?? "Unknown error"}\n";
                File.AppendAllText(logPath, message);
                MessageBox.Show($"Error: {ex?.Message}\nLog saved to Desktop", "WinPanel Error");
            }
            catch { }
        }
    }
}
