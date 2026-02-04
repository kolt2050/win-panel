using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WinPanel.Services
{
    public class AppConfig
    {
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public double Opacity { get; set; } = 45;
        public double Scale { get; set; } = 100;
        public bool IsVertical { get; set; } = false;
        public List<string> ShortcutPaths { get; set; } = new();
    }

    public class ConfigService
    {
        private readonly string _configPath;
        public string ShortcutsDirectory { get; }

        public ConfigService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configDir = Path.Combine(appData, "WinPanel");
            ShortcutsDirectory = Path.Combine(configDir, "Shortcuts");
            
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            if (!Directory.Exists(ShortcutsDirectory))
            {
                Directory.CreateDirectory(ShortcutsDirectory);
            }
            
            _configPath = Path.Combine(configDir, "config.json");
        }

        public AppConfig Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch
            {
            }
            return new AppConfig();
        }

        public void Save(AppConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(_configPath, json);
            }
            catch
            {
            }
        }
    }
}
