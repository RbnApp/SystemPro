namespace RbnSystemPro.Models
{
    public class GpuDriverModel
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty; // NVIDIA, AMD, Intel
        public string Description { get; set; } = string.Empty;
        public string IconSymbol { get; set; } = "DeveloperBoard24";
        public string HexColor { get; set; } = "#76B900";
        
        // Installation status
        public bool IsInstalled { get; set; }
        public string InstalledVersion { get; set; } = "Not Installed";
        public string LatestVersion { get; set; } = "Unknown";
        public bool UpdateAvailable { get; set; }
        
        // Driver Details
        public string DriverType { get; set; } = "Standard"; // Game Ready, Studio, WHQL, etc.
        public string DownloadUrl { get; set; } = string.Empty;

        // Detection
        public bool IsCurrentGpu { get; set; } // Mevcut sistemde bu GPU var mı?
        // Installed-package name fragments used when the driver has no winget package id
        public string[] DetectionNames { get; set; } = System.Array.Empty<string>();
    }
}
