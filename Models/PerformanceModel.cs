using System.Collections.Generic;
using RbnSystemPro.Services;

namespace RbnSystemPro.Models
{
    public class PerformanceModel
    {
        // null = value could not be read (no sensor / WMI unavailable) -> the UI shows "—"
        public double? CpuUsage { get; set; }
        public double? GpuUsage { get; set; }
        public double RamUsagePercentage { get; set; }
        public double RamUsedGB { get; set; }
        public double RamTotalGB { get; set; }
        private System.TimeSpan? _uptime;
        private string _powerPlan = string.Empty;

        // DAPP-41: süre bileşenleri saklanır, metin okuma anında çevrilir (dil değişince de doğru).
        public System.TimeSpan? UptimeValue
        {
            get => _uptime;
            set => _uptime = value;
        }

        public string Uptime => _uptime is { } u
            ? Loc.T("perf.uptimeFormat", (int)u.TotalDays, u.Hours, u.Minutes)
            : Loc.T("common.unknown");

        public string PowerPlan
        {
            get => string.IsNullOrWhiteSpace(_powerPlan) ? Loc.T("common.unknown") : _powerPlan;
            set => _powerPlan = value;
        }
        public List<DiskDriveInfo> Disks { get; set; } = new();

        public string CpuUsageText => CpuUsage.HasValue ? $"{CpuUsage.Value:F0}%" : "—";
        public string GpuUsageText => GpuUsage.HasValue ? $"{GpuUsage.Value:F0}%" : "—";
        // ProgressBar.Value is a non-nullable double
        public double CpuUsageValue => CpuUsage ?? 0;
        public double GpuUsageValue => GpuUsage ?? 0;
    }

    public class DiskDriveInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        private string _model = string.Empty;

        public string Model
        {
            get => string.IsNullOrWhiteSpace(_model) ? Loc.T("perf.genericDrive") : _model;
            set => _model = value;
        }
        public string PhysicalDiskId { get; set; } = string.Empty;
        // null = not reported by the hardware. No invented scores.
        public int? Health { get; set; }
        public int? Performance { get; set; }
        public int? Temperature { get; set; }
        /// <summary>Durum anahtarı ("perf.ok" / "perf.notOk"); null ise durum bilinmiyor.</summary>
        public string? DriveStatusKey { get; set; }
        public bool IsSsd { get; set; } = true;
        public bool IsSystemDrive { get; set; } = false;
        public bool IsPartition { get; set; } = false;
        public string IconSymbol => IsCloudDrive ? "Cloud24" : (IsSsd ? "Flash24" : "HardDrive20");
        public bool IsCloudDrive => Label.Contains("Google Drive", StringComparison.OrdinalIgnoreCase) ||
                                    Label.Contains("OneDrive", StringComparison.OrdinalIgnoreCase);
        
        public double TotalSizeGB { get; set; }
        public double FreeSpaceGB { get; set; }
        public double UsedSpaceGB => TotalSizeGB - FreeSpaceGB;
        public double UsagePercentage => TotalSizeGB > 0 ? (UsedSpaceGB / TotalSizeGB) * 100 : 0;
        public string UsageText => Loc.T("perf.usageOf", UsedSpaceGB, TotalSizeGB);
        public string TemperatureText => Temperature.HasValue ? $"{Temperature.Value}°C" : "—";
        public string StatusText => string.IsNullOrEmpty(DriveStatusKey) ? "—" : Loc.T(DriveStatusKey);
        public string Format { get; set; } = string.Empty;
    }
}
