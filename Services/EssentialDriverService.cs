using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using RbnSystemPro.Models;
using RbnSystemPro.Helpers;

namespace RbnSystemPro.Services
{
    public interface IEssentialDriverService
    {
        List<EssentialDriverModel> GetEssentialDrivers();
        Task CheckInstalledDriversAsync(List<EssentialDriverModel> drivers);
        Task<bool> InstallDriverAsync(EssentialDriverModel driver);
    }

    public class EssentialDriverModel : ObservableObject
    {
        private string _name = string.Empty;
        private string _id = string.Empty;
        private string _description = string.Empty;
        private string _status = "Ready";
        private bool _isInstalled;
        private string _icon = string.Empty;
        private string _hexColor = string.Empty;

        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public string Id { get => _id; set => SetProperty(ref _id, value); }
        public string Description { get => _description; set => SetProperty(ref _description, value); }
        public string Status { get => _status; set => SetProperty(ref _status, value); }
        public bool IsInstalled { get => _isInstalled; set => SetProperty(ref _isInstalled, value); }
        public string Icon { get => _icon; set => SetProperty(ref _icon, value); }
        public string HexColor { get => _hexColor; set => SetProperty(ref _hexColor, value); }
        public string[] DetectionKeywords { get; set; } = Array.Empty<string>();
    }

    public class EssentialDriverService : IEssentialDriverService
    {
        private readonly ILoggerService _logger;
        private readonly ITimelineService _timeline;

        public EssentialDriverService(ILoggerService logger, ITimelineService timeline)
        {
            _logger = logger;
            _timeline = timeline;
        }

        public List<EssentialDriverModel> GetEssentialDrivers()
        {
            return new List<EssentialDriverModel>
            {
                new()
                {
                    Name = "Realtek Audio",
                    Id = "", // no winget package exists for this device driver (verified: winget search --id, 0 results)
                    Description = "High Definition Audio Drivers.",
                    Icon = "Speaker220",
                    HexColor = "#107C10",
                    DetectionKeywords = new[] { "Realtek High Definition Audio", "Realtek(R) Audio", "Realtek Audio" }
                },
                new()
                {
                    Name = "Intel Chipset",
                    Id = "Intel.IntelDriverAndSupportAssistant", // verified with winget search --id --exact
                    Description = "INF Update Utility & Support Assistant for Intel Chipsets.",
                    Icon = "DeveloperBoard24",
                    HexColor = "#0078D7",
                    DetectionKeywords = new[] { "Intel(R) Chipset", "Intel(R) Management Engine", "Intel(R) Serial IO", "Intel(R) SMBus" }
                },
                new()
                {
                    Name = "Intel Management Engine",
                    Id = "Intel.IntelDriverAndSupportAssistant", // verified with winget search --id --exact
                    Description = "IME Component for system stability.",
                    Icon = "ShieldLock24",
                    HexColor = "#0078D7",
                    DetectionKeywords = new[] { "Intel(R) Management Engine Interface", "Intel(R) MEI", "Management Engine" }
                },
                new()
                {
                    Name = "Realtek Ethernet",
                    Id = "", // no winget package exists for this device driver (verified: winget search --id, 0 results)
                    Description = "PCIe GbE / 2.5GbE LAN Controller Drivers.",
                    Icon = "Globe24",
                    HexColor = "#107C10",
                    DetectionKeywords = new[] { "Realtek PCIe", "Realtek Gaming", "Realtek USB GbE", "Realtek RTL" }
                },
                new()
                {
                    Name = "Intel Wireless",
                    Id = "Intel.IntelDriverAndSupportAssistant", // verified with winget search --id --exact
                    Description = "Wi-Fi Drivers for Intel Wireless Adapters.",
                    Icon = "Wifi124",
                    HexColor = "#0078D7",
                    DetectionKeywords = new[] { "Intel(R) Wi-Fi", "Intel(R) Wireless", "Intel(R) Dual Band Wireless" }
                }
            };
        }

        public async Task CheckInstalledDriversAsync(List<EssentialDriverModel> drivers)
        {
            await Task.Run(() =>
            {
                try
                {
                    var deviceNames = new List<string>();

                    using (var searcher = new ManagementObjectSearcher("SELECT Name, Description FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            var name = obj["Name"]?.ToString();
                            var desc = obj["Description"]?.ToString();
                            if (!string.IsNullOrEmpty(name)) deviceNames.Add(name);
                            if (!string.IsNullOrEmpty(desc)) deviceNames.Add(desc);
                        }
                    }

                    foreach (var driver in drivers)
                    {
                        bool installed = driver.DetectionKeywords.Any(kw =>
                            deviceNames.Any(dev => dev.Contains(kw, StringComparison.OrdinalIgnoreCase)));

                        if (installed)
                        {
                            driver.IsInstalled = true;
                            driver.Status = "Installed";
                        }
                        else
                        {
                            driver.IsInstalled = false;
                            driver.Status = "Ready";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogErrorAsync("Failed to check installed essential drivers", ex);
                }
            });
        }

        public async Task<bool> InstallDriverAsync(EssentialDriverModel driver)
        {
            try
            {
                await _logger.LogInfoAsync($"Installing driver: {driver.Name}...");

                // Step 1: Trigger Windows PnP device scan first
                var pnpStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = "/scan-devices",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var pnpProcess = System.Diagnostics.Process.Start(pnpStartInfo))
                {
                    if (pnpProcess != null)
                        await pnpProcess.WaitForExitAsync();
                }

                // Step 2: Try winget installation only when a VERIFIED package id is configured.
                // (Realtek audio/NIC drivers have no winget package; an empty id would just fail.)
                if (!string.IsNullOrEmpty(driver.Id))
                {
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "winget",
                        Arguments = $"install -e --id {driver.Id} --silent --accept-package-agreements --accept-source-agreements",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(processStartInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();

                        if (process.ExitCode == 0)
                        {
                            _timeline.AddEvent($"{driver.Name} Installed", $"Driver {driver.Name} installed successfully.", TimelineEventType.Success);
                            return true;
                        }
                    }
                }

                // Step 3: Re-check if PnPUtil scan resolved the device driver
                var checkList = new List<EssentialDriverModel> { driver };
                await CheckInstalledDriversAsync(checkList);
                if (driver.IsInstalled)
                {
                    _timeline.AddEvent($"{driver.Name} Verified", $"Driver {driver.Name} is active on the system.", TimelineEventType.Success);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Failed to install driver {driver.Name}", ex);
                return false;
            }
        }
    }
}
