using System;
using RbnSystemPro.Services;

namespace RbnSystemPro.Models
{
    public class GpuDriverModel
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty; // NVIDIA, AMD, Intel

        // DAPP-41: açıklama anahtar olarak tutulur; dil değişince ekran yeniden çizer.
        public string DescriptionKey { get; set; } = string.Empty;
        public string DescriptionText => string.IsNullOrEmpty(DescriptionKey) ? string.Empty : Loc.T(DescriptionKey);

        public string IconSymbol { get; set; } = "DeveloperBoard24";
        public string HexColor { get; set; } = "#76B900";

        // Installation status
        public bool IsInstalled { get; set; }

        /// <summary>Ham sürüm numarası (ör. "566.36"). Bilinmiyorsa null.</summary>
        public string? InstalledVersion { get; set; }

        /// <summary>Ham sürüm yokken gösterilecek metnin anahtarı.</summary>
        public string InstalledVersionKey { get; set; } = "gpu.notInstalled";

        public string InstalledVersionText =>
            string.IsNullOrWhiteSpace(InstalledVersion) ? Loc.T(InstalledVersionKey) : InstalledVersion!;

        /// <summary>Karşılaştırma için gerçek sürüm numarası okunabildi mi?</summary>
        public bool HasKnownVersion => !string.IsNullOrWhiteSpace(InstalledVersion);

        public string? LatestVersion { get; set; }
        public string LatestVersionKey { get; set; } = "common.unknown";

        public string LatestVersionText =>
            string.IsNullOrWhiteSpace(LatestVersion) ? Loc.T(LatestVersionKey) : LatestVersion!;

        public bool UpdateAvailable { get; set; }

        // Driver Details
        public string DriverType { get; set; } = "Standard"; // Game Ready, Studio, WHQL, etc.
        public string DownloadUrl { get; set; } = string.Empty;

        // Detection
        public bool IsCurrentGpu { get; set; } // Mevcut sistemde bu GPU var mı?
        // Installed-package name fragments used when the driver has no winget package id
        public string[] DetectionNames { get; set; } = Array.Empty<string>();
    }
}
