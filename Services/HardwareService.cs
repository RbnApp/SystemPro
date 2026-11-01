using RbnSystemPro.Models;
using RbnSystemPro.Helpers;
using System;
using System.Management;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace RbnSystemPro.Services
{
    public class HardwareService : IHardwareService
    {
        private readonly ISettingsService _settingsService;
        private readonly ILoggerService _logger;
        private HardwareInfo? _cachedStaticInfo;
        private readonly object _cacheLock = new();

        public HardwareService(ISettingsService settingsService, ILoggerService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public Task<HardwareInfo> GetSystemInfoAsync()
        {
            return Task.Run(() =>
            {
                HardwareInfo info;
                lock (_cacheLock)
                {
                    if (_cachedStaticInfo != null)
                    {
                        info = new HardwareInfo
                        {
                            CpuName = _cachedStaticInfo.CpuName,
                            RamTotal = _cachedStaticInfo.RamTotal,
                            Motherboard = _cachedStaticInfo.Motherboard,
                            GpuName = _cachedStaticInfo.GpuName,
                            GpuMemory = _cachedStaticInfo.GpuMemory,
                            BiosVersion = _cachedStaticInfo.BiosVersion,
                            BiosDate = _cachedStaticInfo.BiosDate,
                            NumberOfCores = _cachedStaticInfo.NumberOfCores,
                            NumberOfLogicalProcessors = _cachedStaticInfo.NumberOfLogicalProcessors,
                            RamSlots = new System.Collections.Generic.List<string>(_cachedStaticInfo.RamSlots),
                            IsWindowsActivated = _cachedStaticInfo.IsWindowsActivated,
                            WindowsEdition = _cachedStaticInfo.WindowsEdition,
                            BiosStatus = _cachedStaticInfo.BiosStatus,
                            BiosUpdateUrl = _cachedStaticInfo.BiosUpdateUrl,
                            IsBiosUpdateAvailable = _cachedStaticInfo.IsBiosUpdateAvailable
                        };

                        if (_settingsService.Settings.EnableDeepHardwareMonitoring)
                        {
                            info.CpuTemperature = ReadCpuTemperature();
                        }
                        return info;
                    }
                }

                info = new HardwareInfo();

                try
                {
                    // CPU Info
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            var rawName = obj["Name"]?.ToString() ?? "Unknown";
                            info.CpuName = HardwareConstants.SimplifyCpuName(rawName);
                            info.NumberOfCores = Convert.ToInt32(obj["NumberOfCores"]);
                            info.NumberOfLogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
                        }
                    }

                    // Motherboard Info
                    using (var searcher = new ManagementObjectSearcher("SELECT Product, Manufacturer FROM Win32_BaseBoard"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            var manufacturer = HardwareConstants.SimplifyManufacturer(obj["Manufacturer"]?.ToString());
                            var product = HardwareConstants.SimplifyMotherboardProduct(obj["Product"]?.ToString());
                            info.Motherboard = $"{manufacturer} {product}";
                        }
                    }

                    // RAM Info - Total
                    ulong totalRamBytes = 0;
                    using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            totalRamBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                            // Use double for display to show precision (e.g., 31.9 GB)
                            double totalGb = totalRamBytes / (1024.0 * 1024.0 * 1024.0);
                            info.RamTotal = $"{Math.Round(totalGb, 1)} GB";
                        }
                    }

                    // RAM Info - Slots
                    using (var searcher = new ManagementObjectSearcher("SELECT Capacity, DeviceLocator, Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory"))
                    {
                        int slotIndex = 1;
                        foreach (var obj in searcher.Get())
                        {
                            var capacity = Convert.ToUInt64(obj["Capacity"]);
                            string rawLocator = obj["DeviceLocator"]?.ToString() ?? $"Slot {slotIndex}";
                            string finalLocator = HardwareConstants.SimplifyRamSlot(rawLocator, slotIndex);

                            // Get speed
                            uint speed = 0;
                            uint ratedSpeed = 0;

                            if (obj["ConfiguredClockSpeed"] != null)
                                speed = Convert.ToUInt32(obj["ConfiguredClockSpeed"]);
                            
                            if (obj["Speed"] != null)
                                ratedSpeed = Convert.ToUInt32(obj["Speed"]);

                            // Fallback to rated if configured is missing
                            if (speed == 0) speed = ratedSpeed;

                            // Fix for DDR (Double Data Rate)
                            // WMI often reports Base Clock (e.g., 1500 MHz), but users expect MT/s (e.g., 3000 MHz).
                            // If the reported speed is roughly half of the rated speed, it's definitely base clock.
                            if (speed > 0 && ratedSpeed > 0 && Math.Abs((long)speed * 2 - ratedSpeed) < 100)
                            {
                                speed *= 2;
                            }
                            // Heuristic: If speed is suspiciously low for modern RAM (e.g. < 4000) and we didn't double it yet, 
                            // it's likely base clock on a DDR system. Modern DDR4/DDR5 base clocks are 1066-3000+.
                            // However, we must be careful not to double it if it's ALREADY effective.
                            // If we have 1500, it's definitely base (Effective 3000).
                            // If we have 3200, it could be Effective DDR4-3200 OR Base DDR5-6400.
                            // Given the user context (1500), we assume values in [800-2000] are base clocks.
                            else if (speed > 0 && speed < 2400) 
                            { 
                                speed *= 2; 
                            }

                            string speedText = speed > 0 ? $" ({speed} MHz)" : "";

                            info.RamSlots.Add($"{finalLocator}: {capacity / (1024 * 1024 * 1024)} GB{speedText}");
                            slotIndex++;
                        }
                    }

                    // GPU Info
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            string gpuName = obj["Name"]?.ToString() ?? "Unknown GPU";
                            
                            // Skip basic render drivers
                            if (gpuName.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)) continue;

                            // Intelligent Selection: Prefer Dedicated GPU over Integrated
                            // If we already have a GPU stored, check if the current one is better.
                            // APUs usually have "Radeon(TM) Graphics" or "Intel(R) UHD". Dedicated have "RTX", "GTX", "RX".
                            bool isCurrentIntegrated = info.GpuName.Contains("Graphics", StringComparison.OrdinalIgnoreCase) || 
                                                       info.GpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                                                       info.GpuName == "Detecting...";

                            bool isNewProbablyDedicated = !gpuName.Contains("Graphics", StringComparison.OrdinalIgnoreCase) && 
                                                          !gpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase);

                            // If we haven't found anything yet (Unknown GPU or Detecting...), OR if we have an integrated one but found a dedicated one:
                            bool isEmptyOrPlaceholder = string.IsNullOrEmpty(info.GpuName) || info.GpuName == "Detecting..." || info.GpuName == "Unknown GPU";
                            
                            if (isEmptyOrPlaceholder || (isCurrentIntegrated && isNewProbablyDedicated))
                            {
                                info.GpuName = gpuName;
                            
                            try 
                            {
                                ulong rawVramBytes = Convert.ToUInt64(obj["AdapterRAM"]);
                                
                                // WMI AdapterRAM is uint32 and caps at 4GB (4294967295 bytes)
                                // We search Registry for accurate VRAM for ALL cards (robust approach)
                                try
                                {
                                    using (var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                                    {
                                        if (baseKey != null)
                                        {
                                            foreach (string subKeyName in baseKey.GetSubKeyNames())
                                            {
                                                if (subKeyName.Length != 4) continue; // Skip non-adapter keys

                                                using (var subKey = baseKey.OpenSubKey(subKeyName))
                                                {
                                                    string? driverDesc = subKey?.GetValue("DriverDesc")?.ToString();
                                                    if (!string.IsNullOrEmpty(driverDesc) && gpuName.Contains(driverDesc, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        // Try 64-bit property first, then 32-bit
                                                        var registryRam = subKey?.GetValue("HardwareInformation.qwMemorySize") 
                                                                         ?? subKey?.GetValue("HardwareInformation.AdapterMemorySize")
                                                                         ?? subKey?.GetValue("HardwareInformation.MemorySize");

                                                        if (registryRam != null)
                                                        {
                                                            rawVramBytes = Convert.ToUInt64(registryRam);
                                                            break; // Found our card
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _ = _logger.LogWarningAsync($"GPU bellek bilgisi registry'den okunamadı: {ex.Message}");
                                }

                                if (rawVramBytes > 0 || totalRamBytes > 0)
                                {
                                    double dedicatedGb = Math.Round(rawVramBytes / (1024.0 * 1024.0 * 1024.0), 1);
                                    
                                    // Windows typically allocates 50% of system RAM as Shared GPU memory
                                    // Using raw bytes for maximum precision matching DxDiag
                                    double sharedGb = Math.Round((totalRamBytes / 2.0) / (1024.0 * 1024.0 * 1024.0), 1);
                                    double totalGb = Math.Round(dedicatedGb + sharedGb, 1);

                                    info.GpuMemory = $"{dedicatedGb} GB Dedicated / {sharedGb} GB Shared (Total: {totalGb} GB)";
                                }
                                else
                                {
                                    info.GpuMemory = "Detection Pending...";
                                }
                            }
                            catch (Exception ex)
                            {
                                _ = _logger.LogWarningAsync($"GPU belleği hesaplanamadı (WMI/registry): {ex.Message}");
                            }
                        }
                    }
                } // End using GPU searcher

                // BIOS Version
                    using (var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            info.BiosVersion = obj["SMBIOSBIOSVersion"]?.ToString() ?? info.BiosVersion;
                            var rawDate = obj["ReleaseDate"]?.ToString();
                            if (!string.IsNullOrEmpty(rawDate) && rawDate.Length >= 8)
                            {
                                // WMI date format: YYYYMMDD...
                                string y = rawDate.Substring(0, 4);
                                string m = rawDate.Substring(4, 2);
                                string d = rawDate.Substring(6, 2);
                                info.BiosDate = $"{d}.{m}.{y}";
                            }
                        }
                    }

                    // CPU Temperature (Best effort - respects EnableDeepHardwareMonitoring setting)
                    if (_settingsService.Settings.EnableDeepHardwareMonitoring)
                    {
                        info.CpuTemperature = ReadCpuTemperature();
                    }

                    // Windows Activation Status - Robust check for any licensed Windows product
                    using (var searcher = new ManagementObjectSearcher("SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE (Name LIKE 'Windows%' OR Description LIKE 'Windows%') AND LicenseStatus = 1"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            // If we find at least one licensed Windows product entry, it's activated
                            info.IsWindowsActivated = true;
                            break;
                        }
                    }

                    // Windows Edition Info
                    using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            info.WindowsEdition = obj["Caption"]?.ToString() ?? "Unknown Windows";
                        }
                    }

                    lock (_cacheLock)
                    {
                        _cachedStaticInfo = info;
                    }
                }
                catch (Exception)
                {
                    // WMI might throw if certain classes aren't available or if not running as admin
                }

                return info;
            });
        }

        private double ReadCpuTemperature()
        {
            double cpuTemp = 0;
            try
            {
                bool foundTemp = false;

                // Method 1: MSAcpi_ThermalZoneTemperature (Standard for many laptops/OEMs)
                try
                {
                    using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            double temp = Convert.ToDouble(obj["CurrentTemperature"]);
                            temp = (temp - 2732) / 10.0; // Kelvin to Celsius
                            if (temp > 0 && temp < 125)
                            {
                                cpuTemp = temp;
                                foundTemp = true;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"CPU sıcaklığı okunamadı (MSAcpi_ThermalZoneTemperature): {ex.Message}");
                }

                // Method 2: ThermalZoneInformation (Performance Counters) - Fallback
                if (!foundTemp)
                {
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation"))
                        {
                            foreach (var obj in searcher.Get())
                            {
                                double temp = Convert.ToDouble(obj["Temperature"]);
                                if (temp > 200) temp -= 273.15;
                                if (temp > 0 && temp < 125)
                                {
                                    cpuTemp = temp;
                                    foundTemp = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = _logger.LogWarningAsync($"CPU sıcaklığı okunamadı (ThermalZoneInformation sayaçları): {ex.Message}");
                    }
                }

                // Method 3: LibreHardwareMonitor (Most reliable for Desktop CPUs like Ryzen)
                if (!foundTemp)
                {
                    try
                    {
                        var computer = new Computer
                        {
                            IsCpuEnabled = true,
                            IsMotherboardEnabled = true,
                            IsControllerEnabled = true
                        };
                        computer.Open();
                        try
                        {
                            bool foundPreferred = false;

                            foreach (var hardware in computer.Hardware)
                            {
                                hardware.Update();

                                if (hardware.HardwareType == HardwareType.Cpu ||
                                    hardware.HardwareType == HardwareType.Motherboard ||
                                    hardware.HardwareType == HardwareType.SuperIO)
                                {
                                    foreach (var sensor in hardware.Sensors)
                                    {
                                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value.Value > 0)
                                        {
                                            double val = (double)sensor.Value.Value;
                                            string name = sensor.Name;

                                            bool isPreferred = name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                                               name.Contains("Average", StringComparison.OrdinalIgnoreCase) ||
                                                               name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
                                                               name.Contains("Tctl", StringComparison.OrdinalIgnoreCase);

                                            if (isPreferred)
                                            {
                                                cpuTemp = val;
                                                foundTemp = true;
                                                foundPreferred = true;
                                                break;
                                            }
                                            else if (!foundPreferred)
                                            {
                                                if (!foundTemp || val > cpuTemp)
                                                {
                                                    cpuTemp = val;
                                                    foundTemp = true;
                                                }
                                            }
                                        }
                                    }
                                }
                                if (foundPreferred) break;
                            }
                        }
                        finally
                        {
                            computer.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = _logger.LogWarningAsync($"CPU sıcaklığı okunamadı (LibreHardwareMonitor): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"CPU sıcaklığı okunurken beklenmeyen hata: {ex.Message}");
            }

            return cpuTemp;
        }
    }
}
