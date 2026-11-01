namespace RbnSystemPro.Helpers
{
    public static class GlobalConfig
    {
        public const string AppName = "RbnSystemPro";

        // Sürüm: ANA.ARA.YAMA, her basamak 0-9, başında "v" YOK (bkz. README.md → Sürüm).
        public const string AppVersion = "0.1.3";

        public const string VersionUrl = "https://raw.githubusercontent.com/RbnApp/SystemPro/main/version.json";
        public const string ProjectUrl = "https://github.com/RbnApp/SystemPro";

        // Geri bildirim: GitHub'ın "yeni issue" sayfası. Adres TEK yerde burada durur; kullanıcı girdisinden
        // adres kurulmaz (SettingsPage.OnFeedbackClick yalnız bu sabitle çalışır).
        public const string IssuesUrl = ProjectUrl + "/issues/new";

        // Windows Deploy'un çalıştırdığı üçüncü taraf betik (MIT lisanslı). Adres TEK yerde burada durur;
        // Hakkında bölümündeki atıf bağlantısı yalnız bu sabitle çalışır (kullanıcı girdisinden adres kurulmaz).
        public const string Win11DebloatUrl = "https://github.com/Raphire/Win11Debloat";

        // Ekranda gösterilen birleşik ad: "RbnSystemPro 0.1.3"
        public static string FullTitle => $"{AppName} {AppVersion}";

        // Windows sürümü metni: "Windows 11 10.0.26200" (11/10 ayrımı derleme numarasından; 22000 ve üstü 11).
        // Kişisel bilgi içermez: makine adı, kullanıcı adı, yol yok.
        public static string WindowsVersion
        {
            get
            {
                var v = Environment.OSVersion.Version;
                return $"Windows {(v.Build >= 22000 ? "11" : "10")} {v.ToString(3)}";
            }
        }

        // Geri bildirim adresi: önceden doldurulan tek şey sürüm + Windows sürümü (kimlik, makine adı, log yok).
        public static string FeedbackUrl =>
            IssuesUrl + "?body=" + Uri.EscapeDataString($"Sürüm: {AppVersion} / {WindowsVersion}\n\n");
    }
}
