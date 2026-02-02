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
        public double Opacity { get; set; } = 80;
        public bool IsVertical { get; set; } = false;
        public List<string> ShortcutPaths { get; set; } = new();
    }

    public class ConfigService
    {
        private readonly string _configPath;

        public ConfigService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configDir = Path.Combine(appData, "WinPanel");
            
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
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
