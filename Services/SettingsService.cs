using System;
using System.IO;
using System.Text.Json;

namespace RbnSystemPro.Services
{
    public class AppSettings
    {
        public bool EnableDeepHardwareMonitoring { get; set; } = true;
        // Add more settings here as needed
    }

    public interface ISettingsService
    {
        AppSettings Settings { get; }
        void Save();
        void Load();
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsPath;
        private readonly ILoggerService _logger;
        public AppSettings Settings { get; private set; } = new AppSettings();

        public SettingsService(ILoggerService logger)
        {
            _logger = logger;
            var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            
            _settingsPath = Path.Combine(configDir, "app_settings.json");
            Load();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Ayar dosyası yazılamadı: {ex.Message}");
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        Settings = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Settings = new AppSettings();
                _ = _logger.LogWarningAsync($"Ayar dosyası okunamadı, varsayılanlarla devam ediliyor: {ex.Message}");
            }
        }
    }
}
