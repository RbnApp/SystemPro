using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using RbnSystemPro.Models;

namespace RbnSystemPro.Services
{
    public interface IPerformanceService
    {
        Task<PerformanceModel> GetPerformanceDataAsync();
    }

    public class PerformanceService : IPerformanceService
    {
        private readonly ILoggerService _logger;
        private readonly ISettingsService _settingsService;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] MEMORYSTATUSEX lpBuffer);

        public PerformanceService(ILoggerService logger, ISettingsService settingsService)
        {
            _logger = logger;
            _settingsService = settingsService;
        }

        /// <summary>
        /// Extracts the engine type ("3D", "Copy", "VideoDecode", "Security_1", ...) from a
        /// Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine instance name such as
        /// "pid_11212_luid_0x00000000_0x00013D17_phys_0_eng_0_engtype_3D".
        /// </summary>
        private static readonly object DiskMapLock = new();
        private static DateTime _diskMapAt = DateTime.MinValue;
        private static readonly TimeSpan DiskMapTtl = TimeSpan.FromMinutes(5);
        private static Dictionary<string, string> _diskModelMap = new();
        private static Dictionary<string, bool> _diskSsdMap = new();
        private static Dictionary<string, bool> _diskStatusMap = new();

        private static (Dictionary<string, string> Model, Dictionary<string, bool> Ssd, Dictionary<string, bool> StatusOk)? TryGetCachedDiskMap()
        {
            lock (DiskMapLock)
            {
                if (_diskModelMap.Count == 0 || DateTime.UtcNow - _diskMapAt >= DiskMapTtl) return null;
                return (_diskModelMap, _diskSsdMap, _diskStatusMap);
            }
        }

        private static void StoreDiskMap(Dictionary<string, string> model, Dictionary<string, bool> ssd, Dictionary<string, bool> statusOk)
        {
            lock (DiskMapLock)
            {
                _diskModelMap = model;
                _diskSsdMap = ssd;
                _diskStatusMap = statusOk;
                _diskMapAt = DateTime.UtcNow;
            }
        }

        private static string ParseGpuEngineType(string instanceName)
        {
            const string marker = "engtype_";
            int index = instanceName.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return "Unknown";

            string engineType = instanceName.Substring(index + marker.Length).Trim();
            return string.IsNullOrEmpty(engineType) ? "Unknown" : engineType;
        }

        public async Task<PerformanceModel> GetPerformanceDataAsync()
        {
            var model = new PerformanceModel();
            bool deepMonitoring = _settingsService.Settings.EnableDeepHardwareMonitoring;

            await Task.Run(() =>
            {
                // 1. Get CPU & GPU Usage
                try
                {
                    using var cpuSearcher = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
                    foreach (var obj in cpuSearcher.Get()) model.CpuUsage = Convert.ToDouble(obj["PercentProcessorTime"]);

                    if (deepMonitoring)
                    {
                        // Correct class is Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine
                        // (the "_GPUPerformanceAdapter_" spelling does not exist and returns "Invalid class").
                        // Instances are per (process, engine) and the Name carries the engine type, e.g.
                        //   pid_1234_luid_0x..._phys_0_eng_0_engtype_3D
                        // Task Manager semantics: sum each engine type over all processes, then report the
                        // busiest engine type. (The old code summed every instance and divided by 2.)
                        using var gpuSearcher = new ManagementObjectSearcher(
                            "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");

                        var utilizationByEngineType = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        int gpuSamples = 0;
                        foreach (var obj in gpuSearcher.Get())
                        {
                            string instanceName = obj["Name"]?.ToString() ?? string.Empty;
                            double utilization = Convert.ToDouble(obj["UtilizationPercentage"] ?? 0d);
                            string engineType = ParseGpuEngineType(instanceName);

                            utilizationByEngineType[engineType] =
                                utilizationByEngineType.GetValueOrDefault(engineType) + utilization;
                            gpuSamples++;
                        }

                        if (gpuSamples > 0 && utilizationByEngineType.Count > 0)
                        {
                            model.GpuUsage = Math.Min(100.0, utilizationByEngineType.Values.Max());
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"GPU kullanımı okunamadı (GPUEngine sayaçları): {ex.Message}");
                }

                // 2. Get RAM Usage (Reliable API)
                try
                {
                    var memStatus = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(memStatus))
                    {
                        model.RamTotalGB = Math.Round(memStatus.ullTotalPhys / (1024.0 * 1024 * 1024), 1);
                        double usedBytes = memStatus.ullTotalPhys - memStatus.ullAvailPhys;
                        model.RamUsedGB = Math.Round(usedBytes / (1024.0 * 1024 * 1024), 1);
                        model.RamUsagePercentage = memStatus.dwMemoryLoad;
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"Bellek durumu okunamadı (GlobalMemoryStatusEx): {ex.Message}");
                }

                // 3. Get Enhanced Disk Info (Physical Mapping)
                try
                {
                    var driveToModel = new Dictionary<string, string>();
                    var driveToSsd = new Dictionary<string, bool>();
                    var driveToStatusOk = new Dictionary<string, bool>();
                    
                    // H6: disk donanım eşlemesi (model/SSD/durum) süreç ömrü boyunca sabittir.
                    // Önceden bu üç WMI sorgusu HER turda (2 sn'de bir) çalışıyordu; artık 5 dakikalık
                    // önbellekten okunur, WMI yalnız önbellek bayatlayınca çağrılır.
                    var cachedMap = TryGetCachedDiskMap();
                    if (cachedMap != null)
                    {
                        driveToModel = cachedMap.Value.Model;
                        driveToSsd = cachedMap.Value.Ssd;
                        driveToStatusOk = cachedMap.Value.StatusOk;
                    }
                    else
                    {
                        try
                        {
                            var physicalDrives = new Dictionary<string, (string Model, bool Ssd, bool StatusOk)>();
                            using var driveSearcher = new ManagementObjectSearcher("SELECT DeviceID, Model, MediaType, Status FROM Win32_DiskDrive");
                            foreach (var d in driveSearcher.Get())
                            {
                                string deviceId = d["DeviceID"]?.ToString() ?? "";
                                string modelName = d["Model"]?.ToString() ?? Loc.T("perf.physicalDrive");
                                string mediaType = d["MediaType"]?.ToString() ?? "";
                                string status = d["Status"]?.ToString() ?? "OK";
                            
                                bool isSsd = mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) || 
                                             modelName.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                                             modelName.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
                                             modelName.Contains("RC500", StringComparison.OrdinalIgnoreCase) ||
                                             modelName.Contains("SN550", StringComparison.OrdinalIgnoreCase) ||
                                             modelName.Contains("SN750", StringComparison.OrdinalIgnoreCase) ||
                                             modelName.Contains("SN850", StringComparison.OrdinalIgnoreCase) ||
                                             modelName.Contains("Samsung", StringComparison.OrdinalIgnoreCase) ||
                                             modelName.Contains("Crucial", StringComparison.OrdinalIgnoreCase);

                                bool isOk = status.Equals("OK", StringComparison.OrdinalIgnoreCase);
                                string normalizedId = deviceId.Replace("\\", "").Replace(".", "").ToLower();
                                physicalDrives[normalizedId] = (modelName, isSsd, isOk);
                            }

                            var partToPhysical = new Dictionary<string, string>();
                            using var partSearcher = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_DiskDriveToDiskPartition");
                            foreach (var p in partSearcher.Get())
                            {
                                string? ant = p["Antecedent"]?.ToString();
                                string? dep = p["Dependent"]?.ToString();
                                if (ant != null && dep != null)
                                {
                                    string driveId = ant.Split('=')[1].Trim('\"').Replace("\\", "").Replace(".", "").ToLower();
                                    string partId = dep.Split('=')[1].Trim('\"');
                                    partToPhysical[partId] = driveId;
                                }
                            }

                            using var logicalSearcher = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition");
                            foreach (var obj in logicalSearcher.Get())
                            {
                                string? ant = obj["Antecedent"]?.ToString();
                                string? dep = obj["Dependent"]?.ToString();
                                if (ant != null && dep != null)
                                {
                                    string partId = ant.Split('=')[1].Trim('\"');
                                    string logicalName = dep.Split('=')[1].Trim('\"');
                                
                                    if (partToPhysical.TryGetValue(partId, out string? normPhysId) &&
                                        physicalDrives.TryGetValue(normPhysId, out var physInfo))
                                    {
                                        driveToModel[logicalName] = physInfo.Model;
                                        driveToSsd[logicalName] = physInfo.Ssd;
                                        driveToStatusOk[logicalName] = physInfo.StatusOk;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _ = _logger.LogWarningAsync($"Disk-WMI eşlemesi kurulamadı (model/SSD bilgisi boş kalacak): {ex.Message}");
                        }
                        StoreDiskMap(driveToModel, driveToSsd, driveToStatusOk);
                    }

                    string systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";

                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
                        {
                            try 
                            {
                                string cleanName = drive.Name.TrimEnd('\\');
                                var diskInfo = new DiskDriveInfo
                                {
                                    Name = drive.Name,
                                    Label = string.IsNullOrEmpty(drive.VolumeLabel) ? Loc.T("perf.localDisk") : drive.VolumeLabel,
                                    TotalSizeGB = Math.Round(drive.TotalSize / (1024.0 * 1024 * 1024), 1),
                                    FreeSpaceGB = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024 * 1024), 1),
                                    Format = drive.DriveFormat,
                                    IsSystemDrive = cleanName.Equals(systemDrive, StringComparison.OrdinalIgnoreCase)
                                };

                                if (driveToModel.TryGetValue(cleanName, out string? modelName))
                                {
                                    diskInfo.Model = modelName;
                                    diskInfo.IsSsd = driveToSsd.GetValueOrDefault(cleanName, true);
                                    if (diskInfo.IsSystemDrive) diskInfo.IsSsd = true;
                                }

                                // Report only what WMI actually returned. The old code invented
                                // Health (100/50) and Performance (100/90) scores and a 0 °C
                                // temperature for drives without a sensor; those stay null now and
                                // the UI shows "—".
                                if (driveToStatusOk.TryGetValue(cleanName, out bool statusOk))
                                {
                                    diskInfo.Health = statusOk ? 100 : (int?)null;
                                    diskInfo.DriveStatusKey = statusOk ? "perf.ok" : "perf.notOk";
                                }
                                diskInfo.Temperature = null;

                                model.Disks.Add(diskInfo);
                            }
                            catch (Exception ex)
                            {
                                _ = _logger.LogWarningAsync($"Sürücü bilgisi okunamadı ({drive.Name}): {ex.Message}");
                            }
                        }
                    }

                    // Real-time Hardware Data (Temperature & SMART Health)
                    var realTemps = new Dictionary<string, int>();
                    var failingDrives = new Dictionary<string, bool>();
                    
                    var modelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var ssdModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var d in model.Disks)
                    {
                        if (!string.IsNullOrEmpty(d.Model))
                            modelCounts[d.Model] = modelCounts.GetValueOrDefault(d.Model, 0) + 1;
                        if (d.IsSsd || d.IsSystemDrive) ssdModels.Add(d.Model);
                    }

                    if (deepMonitoring)
                    {
                        try
                        {
                            using var tempSearcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM MSStorageDriver_Temperature");
                            foreach (var t in tempSearcher.Get())
                            {
                                string instance = t["InstanceName"]?.ToString()?.ToUpper() ?? "";
                                int rawTemp = Convert.ToInt32(t["CurrentTemperature"]);
                                
                                int finalTemp = rawTemp;
                                if (rawTemp > 500)
                                    finalTemp = (int)(rawTemp / 10.0 - 273.15);
                                else if (rawTemp > 100)
                                    finalTemp = rawTemp > 200 ? rawTemp - 273 : rawTemp;

                                if (finalTemp > 0 && finalTemp < 120)
                                    realTemps[instance] = finalTemp;
                            }

                            using var failSearcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM MSStorageDriver_FailurePredictStatus");
                            foreach (var f in failSearcher.Get())
                            {
                                string instance = f["InstanceName"]?.ToString()?.ToUpper() ?? "";
                                bool predict = Convert.ToBoolean(f["PredictFailure"]);
                                failingDrives[instance] = predict;
                            }
                        }
                        catch (Exception ex)
                        {
                            _ = _logger.LogWarningAsync($"Sıcaklık/SMART verisi okunamadı (root WMI): {ex.Message}");
                        }
                    }

                    foreach (var d in model.Disks)
                    {
                        d.IsPartition = !d.IsCloudDrive && !string.IsNullOrEmpty(d.Model) && modelCounts.GetValueOrDefault(d.Model, 0) > 1;
                        if (ssdModels.Contains(d.Model)) d.IsSsd = true;

                        if (deepMonitoring)
                        {
                            var matchedTemp = realTemps.FirstOrDefault(x => x.Key.Contains(d.Model.Replace(" ", "_").ToUpper())).Value;
                            if (matchedTemp == 0) matchedTemp = realTemps.FirstOrDefault(x => x.Key.Contains("DISK")).Value;

                            var isFailing = failingDrives.Any(x => x.Key.Contains(d.Model.Replace(" ", "_").ToUpper()) && x.Value);

                            if (matchedTemp > 0) d.Temperature = matchedTemp;
                            if (isFailing) d.Health = 0; // SMART PredictFailure (real reading)
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"Disk bilgileri bölümü tamamlanamadı: {ex.Message}");
                }

                try
                {
                    // Environment.TickCount64 gives ms since system started, ignoring Fast Startup's kernel hibernation
                    var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                    model.UptimeValue = uptime;
                }
                catch (Exception ex)
                {
                    model.UptimeValue = null;
                    _ = _logger.LogWarningAsync($"Çalışma süresi hesaplanamadı: {ex.Message}");
                }

                // 5. Get Active Power Plan
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2\power", "SELECT ElementName FROM Win32_PowerPlan WHERE IsActive = True");
                    foreach (var obj in searcher.Get()) model.PowerPlan = obj["ElementName"]?.ToString() ?? "Standard";
                }
                catch (Exception ex)
                {
                    model.PowerPlan = "—";
                    _ = _logger.LogWarningAsync($"Güç planı okunamadı: {ex.Message}");
                }
            });

            return model;
        }
    }
}
