using System;
using RbnSystemPro.Helpers;
using RbnSystemPro.Services;

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

    /// <summary>
    /// Etkinlik kaydı. DAPP-41: yeni kayıtlar metni ANAHTAR olarak saklar (TitleKey/DescriptionKey),
    /// böylece dil değişince geçmiş de yeniden çevrilir. Eski kayıtlarda ham metin (Title/Description)
    /// vardır ve olduğu gibi gösterilir (geri uyum).
    /// </summary>
    public class TimelineEvent : ObservableObject
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>Ham metin (eski kayıtlar / anahtarsız mesajlar).</summary>
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>Çeviri anahtarı; doluysa Title/Description yerine bu kullanılır.</summary>
        public string? TitleKey { get; set; }
        public string? DescriptionKey { get; set; }

        public string[] TitleArgs { get; set; } = Array.Empty<string>();
        public string[] DescriptionArgs { get; set; } = Array.Empty<string>();

        public TimelineEventType Type { get; set; } = TimelineEventType.Info;

        public string TitleText => string.IsNullOrEmpty(TitleKey)
            ? Title
            : Loc.T(TitleKey!, (object?[])TitleArgs);

        public string DescriptionText => string.IsNullOrEmpty(DescriptionKey)
            ? Description
            : Loc.T(DescriptionKey!, (object?[])DescriptionArgs);

        public string TimeLabel => Timestamp.ToString("HH:mm");
        public string DateLabel => Timestamp.ToString("dd.MM.yyyy");
    }
}
