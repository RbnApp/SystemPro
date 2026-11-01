namespace RbnSystemPro.Models
{
    public class HardwareInfo
    {
        public string CpuName { get; set; } = "Unknown CPU";
        public string RamTotal { get; set; } = "0 GB";
        public string Motherboard { get; set; } = "Unknown MB";
        public string GpuName { get; set; } = "Unknown GPU";
        public string GpuMemory { get; set; } = "Unknown VRAM";
        public string BiosVersion { get; set; } = "Unknown";
        public string BiosDate { get; set; } = "Unknown";
        public double CpuTemperature { get; set; }
        public int NumberOfCores { get; set; }
        public int NumberOfLogicalProcessors { get; set; }
        public System.Collections.Generic.List<string> RamSlots { get; set; } = new();
        public bool IsWindowsActivated { get; set; }
        public string WindowsEdition { get; set; } = "Unknown";
        
        // BIOS Update
        public string BiosStatus { get; set; } = "Unknown"; // e.g., "Up to Date", "Update Required", "Unknown"
        public string BiosUpdateUrl { get; set; } = string.Empty;
        public bool IsBiosUpdateAvailable { get; set; } = false;
    }
}
