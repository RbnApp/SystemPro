using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace RbnSystemPro.Services
{
    /// <summary>Kullanıcının Ayarlar'dan seçtiği dil.</summary>
    public enum AppLanguage
    {
        System = 0,   // Windows görüntü dili (varsayılan): tr-* → Türkçe, diğer → English
        Turkish = 1,
        English = 2
    }

    /// <summary>
    /// Metin sözlüğünün ve dil değişiminin tek sahibi. XAML tarafı {l:T anahtar} ile
    /// DynamicResource üzerinden okur (canlı güncellenir); kod tarafı Loc.T("anahtar") çağırır.
    /// İki sözlük (en/tr) aynı anahtar kümesini taşımak zorundadır; eksik anahtar
    /// İngilizce karşılığına düşer ve günlüğe bir kez yazılır (çökme yok).
    /// </summary>
    public static class Loc
    {
        private const string EnSource = "/Resources/Strings.en.xaml";
        private const string TrSource = "/Resources/Strings.tr.xaml";

        private static ResourceDictionary? _en;
        private static ResourceDictionary? _tr;
        private static readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _warnLock = new();

        public static AppLanguage Mode { get; private set; } = AppLanguage.System;

        /// <summary>Aktif kültür kodu: "tr" ya da "en".</summary>
        public static string Language => Mode == AppLanguage.Turkish ? "tr" : "en";

        public static CultureInfo Culture => Language == "tr" ? TrCulture : EnCulture;

        private static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");
        private static readonly CultureInfo EnCulture = CultureInfo.GetCultureInfo("en-US");

        public static event EventHandler? LanguageChanged;

        /// <summary>Eksik anahtar uyarısını günlüğe yazmak için (App kurar; null ise Debug'a yazar).</summary>
        public static Action<string>? WarningSink { get; set; }

        /// <summary>
        /// Uygulama kaynakları hazır olduktan sonra çağrılır. Kayıtlı dil tercihini
        /// ("System"/"Turkish"/"English") okur ve sözlüğü uygular.
        /// </summary>
        public static void Initialize(string? savedPreference)
        {
            EnsureDictionaries();
            Mode = Parse(savedPreference);
            ApplyDictionary();
        }

        /// <summary>Kayıt dosyasındaki metin ("System", "Turkish", "English") → enum.</summary>
        public static AppLanguage Parse(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "turkish" or "tr" or "türkçe" => AppLanguage.Turkish,
                "english" or "en" => AppLanguage.English,
                _ => AppLanguage.System
            };
        }

        public static string ToSettingValue(AppLanguage mode) => mode switch
        {
            AppLanguage.Turkish => "Turkish",
            AppLanguage.English => "English",
            _ => "System"
        };

        /// <summary>Windows görüntü dili Türkçe mi? (Sistem modunun varsayılanı.)</summary>
        public static bool SystemPrefersTurkish()
        {
            try
            {
                var ui = CultureInfo.CurrentUICulture;
                return ui.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static AppLanguage EffectiveMode => Mode == AppLanguage.System
            ? (SystemPrefersTurkish() ? AppLanguage.Turkish : AppLanguage.English)
            : Mode;

        /// <summary>Dili değiştirir: sözlüğü değiştirir, kaynakları canlı tazeler, olayı yayar.</summary>
        public static void SetLanguage(AppLanguage mode, bool notify = true)
        {
            EnsureDictionaries();
            var changed = mode != Mode;
            Mode = mode;
            ApplyDictionary();

            // XAML DynamicResource bağları sözlük değişince kendiliğinden tazelenir;
            // kod tarafındaki metinler için olay yayılır.
            if (changed && notify)
            {
                LanguageChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>Anahtarı çevirir. Bulunamazsa İngilizce karşılığı, o da yoksa anahtarın kendisi döner.</summary>
        public static string T(string key, params object?[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            EnsureDictionaries();

            var dict = Language == "tr" ? _tr : _en;
            string? text = null;
            if (dict != null && dict.Contains(key)) text = dict[key] as string;

            if (string.IsNullOrEmpty(text) && dict != _en && _en != null && _en.Contains(key))
            {
                text = _en[key] as string;
                Warn(key);
            }

            if (string.IsNullOrEmpty(text))
            {
                Warn(key);
                return key;
            }

            return args.Length == 0 ? text : string.Format(Culture, text, args);
        }

        /// <summary>Anahtar iki sözlükte de var mı (test/doğrulama için).</summary>
        public static bool HasKey(string key)
        {
            EnsureDictionaries();
            return (_en?.Contains(key) ?? false) && (_tr?.Contains(key) ?? false);
        }

        /// <summary>En ve tr sözlüklerinin anahtar kümeleri (doğrulama betiği ve duman testi için).</summary>
        public static (IReadOnlyCollection<string> En, IReadOnlyCollection<string> Tr) Keys()
        {
            EnsureDictionaries();
            var en = new List<string>();
            var tr = new List<string>();
            if (_en != null) foreach (var k in _en.Keys) en.Add(Convert.ToString(k, CultureInfo.InvariantCulture) ?? "");
            if (_tr != null) foreach (var k in _tr.Keys) tr.Add(Convert.ToString(k, CultureInfo.InvariantCulture) ?? "");
            return (en, tr);
        }

        /// <summary>Bir dil sözlüğünü anahtar→metin olarak okur (yalnız doğrulama/rapor için).</summary>
        public static IReadOnlyDictionary<string, string> DictionaryOf(string language)
        {
            EnsureDictionaries();
            var src = language == "tr" ? _tr : _en;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (src != null)
            {
                foreach (var k in src.Keys)
                {
                    var key = Convert.ToString(k, CultureInfo.InvariantCulture) ?? "";
                    map[key] = src[k] as string ?? "";
                }
            }
            return map;
        }

        private static void Warn(string key)
        {
            lock (_warnLock)
            {
                if (!_warned.Add(key)) return;
            }

            var message = $"Eksik yerelleştirme anahtarı: {key} ({Language})";
            try
            {
                if (WarningSink != null) WarningSink(message);
                else System.Diagnostics.Debug.WriteLine("[RbnSystemPro] " + message);
            }
            catch
            {
                // Uyarı yazılamazsa uygulama çalışmaya devam eder.
            }
        }

        private static void EnsureDictionaries()
        {
            if (_en != null && _tr != null) return;
            _en = Load(EnSource);
            _tr = Load(TrSource);
        }

        private static ResourceDictionary? Load(string source)
        {
            try
            {
                return new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RbnSystemPro] Sözlük yüklenemedi {source}: {ex.Message}");
                return null;
            }
        }

        private static void ApplyDictionary()
        {
            var app = Application.Current;
            if (app == null) return;

            var active = EffectiveMode == AppLanguage.Turkish ? _tr : _en;
            if (active == null) return;

            var merged = app.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var src = merged[i].Source?.OriginalString ?? string.Empty;
                if (src.EndsWith("Strings.en.xaml", StringComparison.OrdinalIgnoreCase) ||
                    src.EndsWith("Strings.tr.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    merged.RemoveAt(i);
                }
            }
            merged.Add(active);
        }
    }

    public interface ILocalizationService
    {
        AppLanguage Language { get; }
        string CurrentCulture { get; }
        event EventHandler? LanguageChanged;
        string T(string key, params object?[] args);
        void SetLanguage(AppLanguage language);
    }

    /// <summary>
    /// DI kaydı (App.xaml.cs). Ayar dosyasındaki "Language" alanını kalıcı tutar;
    /// gerçek çeviri işini Loc yapar (XAML ve DI dışı servisler de aynı sözlüğü görsün diye).
    /// </summary>
    public class LocalizationService : ILocalizationService
    {
        private readonly ISettingsService _settings;
        private readonly ILoggerService _logger;

        public LocalizationService(ISettingsService settings, ILoggerService logger)
        {
            _settings = settings;
            _logger = logger;

            Loc.WarningSink ??= message => _ = _logger.LogWarningAsync(message);
            Loc.Initialize(_settings.Settings.Language);
            Loc.LanguageChanged += (_, _) => LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public AppLanguage Language => Loc.Mode;
        public string CurrentCulture => Loc.Language;
        public event EventHandler? LanguageChanged;

        public string T(string key, params object?[] args) => Loc.T(key, args);

        public void SetLanguage(AppLanguage language)
        {
            if (language == Loc.Mode) return;

            Loc.SetLanguage(language);
            _settings.Settings.Language = Loc.ToSettingValue(language);
            _settings.Save();
            _ = _logger.LogInfoAsync($"Arayüz dili değiştirildi: {Loc.ToSettingValue(language)} (etkin: {Loc.Language})");
        }
    }

    /// <summary>
    /// İlk açılışta dil tercihini, DI kabı kurulmadan okumak için: SettingsService'in
    /// kullandığı dosyanın (Logs/app_settings.json) "Language" alanı.
    /// </summary>
    public static class LanguagePreference
    {
        public static string? ReadFromDisk()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "app_settings.json");
                if (!File.Exists(path)) return null;

                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("Language", out var value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
            catch
            {
                // Bozuk/okunamayan ayar dosyası: sistem diliyle başla.
            }
            return null;
        }
    }
}
