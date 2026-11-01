using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using RbnSystemPro.Models;

namespace RbnSystemPro.Services
{
    public interface ITimelineService
    {
        ObservableCollection<TimelineEvent> Events { get; }
        void AddEvent(string title, string description, TimelineEventType type = TimelineEventType.Info);
        /// <summary>DAPP-41: kayıt anahtar olarak saklanır; dil değişince geçmiş de çevrilir.</summary>
        void AddEventKey(string titleKey, string descriptionKey, TimelineEventType type = TimelineEventType.Info,
            string[]? titleArgs = null, string[]? descriptionArgs = null);
        void Clear();
    }

    public class TimelineService : ITimelineService
    {
        private readonly ILoggerService? _logger;
        public ObservableCollection<TimelineEvent> Events { get; } = new ObservableCollection<TimelineEvent>();
        private readonly string _logFilePath;
        private readonly object _fileLock = new();
        private const int MaxEvents = 50;

        public TimelineService(ILoggerService? logger = null)
        {
            _logger = logger;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logDir = Path.Combine(baseDir, "Logs");

            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

            _logFilePath = Path.Combine(logDir, "activity_log.json");
            LoadEvents();
        }

        public void AddEvent(string title, string description, TimelineEventType type = TimelineEventType.Info)
        {
            var now = DateTime.Now;
            var cutOffDate = now.AddDays(-10);

            var newEvent = new TimelineEvent
            {
                Title = title,
                Description = description,
                Type = type,
                Timestamp = now
            };

            void UpdateCollection()
            {
                Events.Insert(0, newEvent);

                // 1. Remove events older than 10 days
                for (int i = Events.Count - 1; i >= 0; i--)
                {
                    if (Events[i].Timestamp < cutOffDate)
                    {
                        Events.RemoveAt(i);
                    }
                }

                // 2. Keep hard limit (optional safety net)
                while (Events.Count > MaxEvents)
                {
                    Events.RemoveAt(Events.Count - 1);
                }

                SaveEvents();
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(UpdateCollection);
            }
            else
            {
                UpdateCollection();
            }
        }

        public void AddEventKey(string titleKey, string descriptionKey, TimelineEventType type = TimelineEventType.Info,
            string[]? titleArgs = null, string[]? descriptionArgs = null)
        {
            var now = DateTime.Now;
            var cutOffDate = now.AddDays(-10);

            var newEvent = new TimelineEvent
            {
                TitleKey = titleKey,
                DescriptionKey = descriptionKey,
                TitleArgs = titleArgs ?? Array.Empty<string>(),
                DescriptionArgs = descriptionArgs ?? Array.Empty<string>(),
                Type = type,
                Timestamp = now
            };

            void UpdateCollection()
            {
                Events.Insert(0, newEvent);

                for (int i = Events.Count - 1; i >= 0; i--)
                {
                    if (Events[i].Timestamp < cutOffDate) Events.RemoveAt(i);
                }

                while (Events.Count > MaxEvents) Events.RemoveAt(Events.Count - 1);

                SaveEvents();
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(UpdateCollection);
            }
            else
            {
                UpdateCollection();
            }
        }

        public void Clear()
        {
            void ClearCollection()
            {
                Events.Clear();
                SaveEvents();
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(ClearCollection);
            }
            else
            {
                ClearCollection();
            }
        }

        private void SaveEvents()
        {
            try
            {
                lock (_fileLock)
                {
                    var json = JsonSerializer.Serialize(Events.ToList(), new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_logFilePath, json);
                }
            }
            catch (Exception ex)
            {
                // Kendi dosyasına yazamazsa uygulama günlüğüne iz bırakır (sonsuz döngü yok: logger timeline kullanmaz).
                _ = _logger?.LogWarningAsync($"Aktivite geçmişi dosyaya yazılamadı: {ex.Message}");
            }
        }

        private void LoadEvents()
        {
            try
            {
                lock (_fileLock)
                {
                    if (File.Exists(_logFilePath))
                    {
                        var json = File.ReadAllText(_logFilePath);
                        var loadedEvents = JsonSerializer.Deserialize<List<TimelineEvent>>(json);
                        if (loadedEvents != null)
                        {
                            var cutOffDate = DateTime.Now.AddDays(-10);
                            Events.Clear();
                            
                            // Only load events from the last 10 days
                            var filtered = loadedEvents
                                .Where(x => x.Timestamp >= cutOffDate)
                                .OrderByDescending(x => x.Timestamp)
                                .Take(MaxEvents);

                            foreach (var ev in filtered)
                            {
                                Events.Add(ev);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _ = _logger?.LogWarningAsync($"Aktivite geçmişi okunamadı, boş listeyle başlanıyor: {ex.Message}");
            }
        }
    }
}
