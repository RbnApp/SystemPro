using System;
using System.IO;
using System.Threading.Tasks;

namespace RbnSystemPro.Services
{
    public interface ILoggerService
    {
        Task LogInfoAsync(string message);
        Task LogWarningAsync(string message);
        Task LogErrorAsync(string message, Exception? ex = null);
    }

    public class LoggerService : ILoggerService
    {
        private readonly string _logFilePath;
        private readonly object _lock = new();

        public LoggerService()
        {
            // Go up from bin/Debug/net10.0... to the actual project root
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logDir = Path.Combine(baseDir, "Logs");

            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            
            _logFilePath = Path.Combine(logDir, "log.txt");
            Task.Run(CleanOldLogs);
        }

        public async Task LogInfoAsync(string message) => await WriteLogAsync("INFO", message);
        public async Task LogWarningAsync(string message) => await WriteLogAsync("WARNING", message);
        public async Task LogErrorAsync(string message, Exception? ex = null) 
        {
            var content = message;
            if (ex != null)
            {
                content += $"\nException Type: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            await WriteLogAsync("ERROR", content);
        }

        private void CleanOldLogs()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_logFilePath)) return;

                    var cutOffDate = DateTime.Now.AddDays(-10);
                    var lines = File.ReadAllLines(_logFilePath);
                    var freshLines = new List<string>();

                    foreach (var line in lines)
                    {
                        // Log format: [yyyy-MM-dd HH:mm:ss] [LEVEL] Message
                        if (line.StartsWith("[") && line.Length > 20)
                        {
                            var datePart = line.Substring(1, 10);
                            if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var logDate))
                            {
                                if (logDate >= cutOffDate)
                                {
                                    freshLines.Add(line);
                                }
                                continue;
                            }
                        }
                        // If line doesn't follow format or parsing fails, keep it if it's orphaned content
                        if (freshLines.Count > 0) freshLines.Add(line);
                    }

                    if (freshLines.Count < lines.Length)
                    {
                        File.WriteAllLines(_logFilePath, freshLines);
                    }
                }
                catch (Exception ex)
                {
                    // Kendi dosyasını temizlerken oluşan hata kendi günlüğüne yazılamaz; hata ayıklama çıktısına düşer.
                    System.Diagnostics.Debug.WriteLine($"[RbnSystemPro] Log temizliği başarısız: {ex.Message}");
                }
            }
        }

        private async Task WriteLogAsync(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var line = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";

            try
            {
                // Synchronous lock for reliability during crashes
                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, line);
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LOGGER FAIL: {ex.Message}");
            }
        }

        public string GetLogPath() => _logFilePath;
    }
}
