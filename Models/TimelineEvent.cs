using System;

namespace RbnSystemPro.Models
{
    public enum TimelineEventType
    {
        Info,
        Success,
        Warning,
        Error,
        Installation,
        Optimization
    }

    public class TimelineEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TimelineEventType Type { get; set; } = TimelineEventType.Info;
        
        public string TimeLabel => Timestamp.ToString("HH:mm");
        public string DateLabel => Timestamp.ToString("dd.MM.yyyy");
    }
}
