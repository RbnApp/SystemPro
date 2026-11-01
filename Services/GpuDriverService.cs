using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RbnSystemPro.Models;

namespace RbnSystemPro.Services
{
    public interface IGpuDriverService
    {
        Task<List<GpuDriverModel>> GetAvailableDriversAsync(string gpuName = "");
        Task<bool> InstallDriverAsync(GpuDriverModel driver);
        Task<bool> UpdateDriverAsync(GpuDriverModel driver);
        string DetectCurrentGpuManufacturer(string gpuName);
    }

    public class GpuDriverService : IGpuDriverService
    {
        private readonly ILoggerService _logger;
        private readonly ITimelineService _timeline;
        private readonly HttpClient _httpClient;

        public GpuDriverService(ILoggerService logger, ITimelineService timeline)
        {
            _logger = logger;
            _timeline = timeline;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public string DetectCurrentGpuManufacturer(string gpuName)
        {
            if (string.IsNullOrEmpty(gpuName))
                return "Unknown";

            gpuName = gpuName.ToLower();

            if (gpuName.Contains("nvidia") || gpuName.Contains("geforce") || gpuName.Contains("rtx") || gpuName.Contains("gtx"))
                return "NVIDIA";
            
            if (gpuName.Contains("amd") || gpuName.Contains("radeon") || gpuName.Contains("rx "))
                return "AMD";
            
            if (gpuName.Contains("intel") || gpuName.Contains("uhd") || gpuName.Contains("iris"))
                return "Intel";

            return "Unknown";
        }

        public async Task<List<GpuDriverModel>> GetAvailableDriversAsync(string gpuName = "")
        {
            var drivers = new List<GpuDriverModel>();
            string mfr = DetectCurrentGpuManufacturer(gpuName);

            // NVIDIA
            drivers.Add(new GpuDriverModel
            {
                Name = "NVIDIA Game Ready Driver",
                Id = "", // not in winget (verified); NVIDIA is always a manual download
                DriverType = "Game Ready",
                Manufacturer = "NVIDIA",
                DescriptionKey = "gpu.nvidiaGameReady.desc",
                IconSymbol = "DeveloperBoard24",
                HexColor = "#76B900",
                DownloadUrl = "https://www.nvidia.com/Download/index.aspx"
            });
            drivers.Add(new GpuDriverModel
            {
                Name = "NVIDIA Studio Driver",
                Id = "", // not in winget (verified); NVIDIA is always a manual download
                DriverType = "Studio",
                Manufacturer = "NVIDIA",
                DescriptionKey = "gpu.nvidiaStudio.desc",
                IconSymbol = "Shapes24",
                HexColor = "#76B900",
                DownloadUrl = "https://www.nvidia.com/Download/index.aspx?lang=en-us"
            });

            // AMD
            drivers.Add(new GpuDriverModel
            {
                Name = "AMD Adrenalin Driver",
                Id = "", // not in winget (verified); AMD is a manual download
                Manufacturer = "AMD",
                DriverType = "WHQL",
                DescriptionKey = "gpu.amdAdrenalin.desc",
                IconSymbol = "DeveloperBoard24",
                HexColor = "#ED1C24",
                DownloadUrl = "https://www.amd.com/en/support/download/drivers.html",
                DetectionNames = new[] { "AMD Software" }
            });

            // Intel
            drivers.Add(new GpuDriverModel
            {
                Name = "Intel Graphics Driver",
                Id = "", // not in winget (verified); Intel is a manual download
                Manufacturer = "Intel",
                DescriptionKey = "gpu.intelGraphics.desc",
                IconSymbol = "DeveloperBoard24",
                HexColor = "#0071C5",
                DownloadUrl = "https://www.intel.com/content/www/us/en/download-center/home.html",
                DetectionNames = new[] { "Intel® Graphics", "Intel Graphics" }
            });

            // Check installation status and versions for each driver
            foreach (var driver in drivers)
            {
                await CheckDriverStatusAsync(driver, gpuName, drivers);
            }

            return drivers;
        }

        private async Task CheckDriverStatusAsync(GpuDriverModel driver, string gpuName = "", List<GpuDriverModel>? allDrivers = null)
        {
            try
            {
                // For NVIDIA, ONLY check via our robust registry/uninstall scanner
                // Winget ID is shared between branches and causes false positives
                if (driver.Manufacturer == "NVIDIA")
                {
                    await CheckNvidiaFromRegistryAsync(driver, gpuName, allDrivers);
                    return; // Always return here for NVIDIA
                }

                // No winget package id (AMD Adrenalin / Intel Graphics have none): detect the installed
                // package by name instead of querying an invented id.
                if (string.IsNullOrEmpty(driver.Id))
                {
                    await CheckDriverStatusFromPackageNameAsync(driver);
                    return;
                }

                // Check if driver is installed using winget list
                var listStartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"list --id {driver.Id} --accept-source-agreements",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var listProcess = new Process { StartInfo = listStartInfo })
                {
                    listProcess.Start();
                    string listOutput = await listProcess.StandardOutput.ReadToEndAsync();
                    await listProcess.WaitForExitAsync();

                    if (listProcess.ExitCode == 0 && !string.IsNullOrEmpty(listOutput))
                    {
                        // Parse the output to get installed version
                        var lines = listOutput.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains(driver.Id))
                            {
                                driver.IsInstalled = true;
                                
                                // Try to extract version (format varies, this is a basic approach)
                                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 3)
                                {
                                    driver.InstalledVersion = parts[2];
                                    driver.InstalledVersionKey = "status.installed";
                                }
                                break;
                            }
                        }
                    }
                }

                // Check for updates using winget upgrade
                if (driver.IsInstalled)
                {
                    var upgradeStartInfo = new ProcessStartInfo
                    {
                        FileName = "winget",
                        Arguments = $"upgrade --id {driver.Id} --accept-source-agreements",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var upgradeProcess = new Process { StartInfo = upgradeStartInfo })
                    {
                        upgradeProcess.Start();
                        string upgradeOutput = await upgradeProcess.StandardOutput.ReadToEndAsync();
                        await upgradeProcess.WaitForExitAsync();

                        if (!string.IsNullOrEmpty(upgradeOutput))
                        {
                            var lines = upgradeOutput.Split('\n');
                            foreach (var line in lines)
                            {
                                if (line.Contains(driver.Id))
                                {
                                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length >= 4)
                                    {
                                        driver.LatestVersion = parts[3];
                                        driver.UpdateAvailable = driver.InstalledVersion != driver.LatestVersion;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    driver.InstalledVersion = null;
                driver.InstalledVersionKey = "gpu.notInstalled";
                    driver.UpdateAvailable = false;
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error checking driver status for {driver.Name}", ex);
                driver.IsInstalled = false;
                driver.InstalledVersion = null;
                driver.InstalledVersionKey = "gpu.error";
            }
        }

        /// <summary>
        /// Detects an installed driver that has no winget package id by looking it up in
        /// "winget list" with its package name fragments (e.g. "AMD Software").
        /// </summary>
        private static readonly object WingetCacheLock = new();
        private static string? _wingetListCache;
        private static DateTime _wingetListCacheAt = DateTime.MinValue;
        private static readonly TimeSpan WingetListTtl = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Kurulu paketlerin tam listesi (tek `winget list`), 60 saniye önbellekli.
        /// Sayfa her açıldığında ayrı ayrı winget çağırmak yerine bir kez sorgulanır.
        /// </summary>
        private async Task<string?> GetInstalledPackagesListAsync()
        {
            lock (WingetCacheLock)
            {
                if (_wingetListCache != null && DateTime.UtcNow - _wingetListCacheAt < WingetListTtl)
                {
                    return _wingetListCache;
                }
            }

            try
            {
                var listStartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "list --accept-source-agreements",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var listProcess = new Process { StartInfo = listStartInfo };
                listProcess.Start();
                string output = await listProcess.StandardOutput.ReadToEndAsync();
                await listProcess.WaitForExitAsync();

                if (listProcess.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    lock (WingetCacheLock)
                    {
                        _wingetListCache = output;
                        _wingetListCacheAt = DateTime.UtcNow;
                    }
                    return output;
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("winget list okunamadı", ex);
            }

            return null;
        }

        private async Task CheckDriverStatusFromPackageNameAsync(GpuDriverModel driver)
        {
            driver.UpdateAvailable = false;

            if (driver.DetectionNames.Length == 0)
            {
                driver.IsInstalled = false;
                driver.InstalledVersion = null;
                driver.InstalledVersionKey = "gpu.notInstalled";
                return;
            }

            try
            {
                // Sürücü başına ayrı `winget list --name` çağırmak GPU sayfasını 3+ saniyeye
                // çıkarıyordu (denetim H2). Tek çağrı, 60 s önbellek: sayfa açılışları arasında
                // tekrar tekrar sorgulanmaz.
                string? listOutput = await GetInstalledPackagesListAsync();
                if (string.IsNullOrEmpty(listOutput))
                {
                    driver.IsInstalled = false;
                    driver.InstalledVersion = null;
                driver.InstalledVersionKey = "gpu.notInstalled";
                    return;
                }

                foreach (var keyword in driver.DetectionNames)
                {
                    foreach (var line in listOutput.Split('\n'))
                    {
                        if (!TryParseWingetRow(line, out string name, out _, out string version)) continue;
                        if (!name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;

                        driver.IsInstalled = true;
                        driver.InstalledVersion = string.IsNullOrEmpty(version) ? null : version;
                        driver.InstalledVersionKey = "status.installed";
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error checking driver status for {driver.Name}", ex);
                driver.InstalledVersion = null;
                driver.InstalledVersionKey = "gpu.error";
                return;
            }

            driver.IsInstalled = false;
            driver.InstalledVersion = null;
                driver.InstalledVersionKey = "gpu.notInstalled";
        }

        /// <summary>
        /// winget output is column aligned with two or more spaces between Name / Id / Version.
        /// </summary>
        private static bool TryParseWingetRow(string line, out string name, out string id, out string version)
        {
            name = id = version = string.Empty;
            if (string.IsNullOrWhiteSpace(line)) return false;

            var parts = Regex.Split(line.Trim(), @"\s{2,}");
            if (parts.Length < 3) return false;

            name = parts[0].Trim();
            id = parts[1].Trim();
            version = parts[2].Trim();

            if (name.Equals("Name", StringComparison.OrdinalIgnoreCase) || id.Equals("Id", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private async Task CheckNvidiaFromRegistryAsync(GpuDriverModel driver, string gpuName, List<GpuDriverModel>? allDrivers = null)
        {
            try
            {
                // Source 1: Registry (NVIDIA Global GfxDriver)
                string regVersion = "";
                bool isStudioFromReg = false;
                bool foundInReg = false;

                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\NVIDIA Corporation\Global\GfxDriver"))
                    {
                        if (key != null)
                        {
                            var versionObj = key.GetValue("DisplayDriverVersion") ?? key.GetValue("Version");
                            if (versionObj != null)
                            {
                                string rawVersion = versionObj.ToString() ?? "";
                                string cleanVersion = Regex.Replace(rawVersion, @"[^\d]", "");
                                if (cleanVersion.Length >= 5)
                                {
                                    string lastFive = cleanVersion.Substring(cleanVersion.Length - 5);
                                    regVersion = lastFive.Insert(3, ".");
                                    foundInReg = true;
                                }
                            }

                            var typeObj = key.GetValue("NVDriverType");
                            if (typeObj != null)
                            {
                                try {
                                    int typeVal = Convert.ToInt32(typeObj);
                                    isStudioFromReg = typeVal == 1;
                                } catch (Exception ex)
                                {
                                    isStudioFromReg = typeObj.ToString() == "1";
                                    _ = _logger.LogWarningAsync($"NVIDIA sürücü tipi sayıya çevrilemedi (NVDriverType): {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"NVIDIA sürücü sürümü registry'den okunamadı: {ex.Message}");
                }

                // Source 1.5: Detailed Uninstall Registry Scan
                bool studioDetectedInUninstall = false;
                string uninstallVersion = "";

                try
                {
                    string[] uninstallPaths = { 
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" 
                    };

                    foreach (var path in uninstallPaths)
                    {
                        using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path))
                        {
                            if (key == null) continue;
                            foreach (var subkeyName in key.GetSubKeyNames())
                            {
                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey == null) continue;
                                    var displayName = subkey.GetValue("DisplayName")?.ToString();
                                    if (displayName != null && displayName.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        // Check for version here too
                                        var dispVer = subkey.GetValue("DisplayVersion")?.ToString();
                                        if (!string.IsNullOrEmpty(dispVer) && string.IsNullOrEmpty(uninstallVersion))
                                            uninstallVersion = dispVer;

                                        // Aggressive check for Studio
                                        if (displayName.IndexOf("Studio", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                            displayName.IndexOf("SD", StringComparison.Ordinal) >= 0)
                                        {
                                            studioDetectedInUninstall = true;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Bonus check: Look into NVIDIA's own registry for branch info
                    if (!studioDetectedInUninstall)
                    {
                        using (var nvKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\NVIDIA Corporation\Global\GfxDriver"))
                        {
                            if (nvKey != null)
                            {
                                var branchObj = nvKey.GetValue("CurrentBranch");
                                string? branchStr = branchObj?.ToString();
                                if (!string.IsNullOrEmpty(branchStr) && branchStr.IndexOf("vulkan_studio", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    studioDetectedInUninstall = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"NVIDIA kaldırma kayıtları okunamadı (Uninstall registry): {ex.Message}");
                }

                // Source 2: WMI Fallback (Win32_VideoController)
                bool foundInWmi = false;
                string wmiVersion = "";
                bool isStudioFromWmi = false;

                try
                {
                    if (!foundInReg)
                    {
                        var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_VideoController WHERE Name LIKE '%NVIDIA%'");
                        foreach (var obj in searcher.Get())
                        {
                            var versionObj = obj["DriverVersion"];
                            if (versionObj != null)
                            {
                                string rawVersion = versionObj.ToString() ?? "";
                                string cleanVersion = Regex.Replace(rawVersion, @"[^\d]", "");
                                if (cleanVersion.Length >= 5)
                                {
                                    string lastFive = cleanVersion.Substring(cleanVersion.Length - 5);
                                    wmiVersion = lastFive.Insert(3, ".");
                                    foundInWmi = true;
                                }
                            }

                            var caption = obj["Caption"]?.ToString();
                            if (caption != null && caption.IndexOf("Studio", StringComparison.OrdinalIgnoreCase) >= 0)
                                isStudioFromWmi = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"NVIDIA sürücü bilgisi WMI'dan okunamadı (Win32_VideoController): {ex.Message}");
                }

                // Final consolidation
                bool isActuallyInstalled = foundInReg || foundInWmi || !string.IsNullOrEmpty(uninstallVersion);
                if (isActuallyInstalled)
                {
                    string finalVersion = regVersion;
                    if (string.IsNullOrEmpty(finalVersion)) finalVersion = wmiVersion;
                    if (string.IsNullOrEmpty(finalVersion)) finalVersion = uninstallVersion;

                    bool finalIsStudio = isStudioFromReg || studioDetectedInUninstall || isStudioFromWmi;

                    // Apply to the correct driver object
                    if ((finalIsStudio && driver.DriverType == "Studio") || 
                        (!finalIsStudio && driver.DriverType == "Game Ready"))
                    {
                        driver.IsInstalled = true;
                        driver.InstalledVersion = finalVersion;
                        driver.InstalledVersionKey = "status.installed";
                        await _logger.LogInfoAsync($"NVIDIA {driver.DriverType} detected: {finalVersion}");
                    }
                    else
                    {
                        driver.IsInstalled = false;
                        driver.InstalledVersion = null;
                driver.InstalledVersionKey = "gpu.notInstalled";
                        driver.UpdateAvailable = false;
                    }
                }
                else
                {
                    driver.IsInstalled = false;
                    driver.InstalledVersion = null;
                driver.InstalledVersionKey = "gpu.notInstalled";
                    driver.UpdateAvailable = false;
                }

                // ALWAYS fetch the latest version for the specific branch (Studio vs Game Ready)
                var latestVersion = await GetLatestNvidiaDriverVersionAsync(gpuName, driver.DriverType);
                if (!string.IsNullOrEmpty(latestVersion))
                {
                    driver.LatestVersion = latestVersion;
                    
                    if (driver.IsInstalled && driver.HasKnownVersion)
                    {
                        if (CompareVersions(driver.InstalledVersion!, latestVersion) < 0)
                        {
                            driver.UpdateAvailable = true;
                        }
                    }
                }
                else if (driver.IsInstalled)
                {
                    // Fallback: If we can't reach the API, at least don't leave it as "Unknown" 
                    // if we just detected a very recent driver.
                    driver.LatestVersion = driver.InstalledVersion;
                    driver.UpdateAvailable = false;
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error checking NVIDIA driver: {ex.Message}", ex);
            }
        }

        private int CompareVersions(string version1, string version2)
        {
            try
            {
                // Extract major version numbers (e.g., "576.80" -> 576, "591.86" -> 591)
                var v1Match = Regex.Match(version1, @"(\d+)\.(\d+)");
                var v2Match = Regex.Match(version2, @"(\d+)\.(\d+)");
                
                if (v1Match.Success && v2Match.Success)
                {
                    int v1Major = int.Parse(v1Match.Groups[1].Value);
                    int v1Minor = int.Parse(v1Match.Groups[2].Value);
                    int v2Major = int.Parse(v2Match.Groups[1].Value);
                    int v2Minor = int.Parse(v2Match.Groups[2].Value);
                    
                    if (v1Major != v2Major)
                        return v1Major.CompareTo(v2Major);
                    return v1Minor.CompareTo(v2Minor);
                }
            }
            catch
            {
                // If comparison fails, assume equal
            }
            
            return 0;
        }

        private async Task<string?> GetLatestNvidiaDriverVersionAsync(string gpuName, string? driverType = "Game Ready")
        {
            try
            {
                // Extract GPU series from name (e.g., "RTX 5060" -> "50")
                var seriesMatch = Regex.Match(gpuName, @"(RTX|GTX)\s*(50|40|30|20|16|10)", RegexOptions.IgnoreCase);
                string seriesBase = seriesMatch.Success ? seriesMatch.Groups[2].Value : "";
                
                bool isLaptop = gpuName.IndexOf("Laptop", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                gpuName.IndexOf("Notebook", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                gpuName.IndexOf("Mobile", StringComparison.OrdinalIgnoreCase) >= 0;

                // Determine product family ID (PFID) and product series ID (PSID)
                int psid = isLaptop ? 102 : 101;
                int pfid = 929; // Default: RTX 30 Series

                if (seriesBase == "50")
                    pfid = isLaptop ? 128 : 127; // GeForce RTX 50 Series
                else if (seriesBase == "40")
                    pfid = isLaptop ? 121 : 120; // GeForce RTX 40 Series
                else if (seriesBase == "30")
                    pfid = isLaptop ? 930 : 929; // GeForce RTX 30 Series
                else if (seriesBase == "20")
                    pfid = isLaptop ? 903 : 902; // GeForce RTX 20 Series
                else if (seriesBase == "16")
                    pfid = isLaptop ? 884 : 883; // GeForce GTX 16 Series
                else if (seriesBase == "10")
                    pfid = isLaptop ? 880 : 879; // GeForce GTX 10 Series
                else if (gpuName.IndexOf("Quadro", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Fallback for Quadro or other professional GPUs if series not matched
                    psid = 100; // Quadro/NVS
                    pfid = 87; // Quadro RTX Series (a common modern one)
                }
                else
                {
                    // Generic fallback if no specific series is matched
                    psid = 101; // Desktop
                    pfid = 929; // GeForce RTX 30 Series (a common modern one)
                }

                await _logger.LogInfoAsync($"NVIDIA API Lookup -> GPU: {gpuName}, Base: {seriesBase}, Laptop: {isLaptop}, PFID: {pfid}, PSID: {psid}, Type: {driverType}");

                int upCRD = (driverType == "Studio") ? 1 : 0;

                // Windows 11 (135) is more likely for RTX 50 cards, fallback to Windows 10 (57)
                int[] osIds = { 135, 57 };
                
                foreach (var osId in osIds)
                {
                    var apiUrl = $"https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid={psid}&pfid={pfid}&osID={osId}&languageCode=1033&beta=0&isWHQL=1&dltype=1&dch=1&upCRD={upCRD}&qnf=0&sort1=0&numberOfResults=1";
                    
                    try
                    {
                        var response = await _httpClient.GetAsync(apiUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            using (JsonDocument doc = JsonDocument.Parse(content))
                            {
                                var root = doc.RootElement;
                                if (root.TryGetProperty("IDS", out var ids) && ids.GetArrayLength() > 0)
                                {
                                    var firstDriver = ids[0];
                                    if (firstDriver.TryGetProperty("downloadInfo", out var downloadInfo))
                                    {
                                        if (downloadInfo.TryGetProperty("Version", out var version))
                                        {
                                            var versionStr = version.GetString();
                                            if (versionStr != null)
                                            {
                                                await _logger.LogInfoAsync($"Latest NVIDIA driver version from API (OSID {osId}): {versionStr}");
                                                return versionStr;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await _logger.LogErrorAsync($"Error fetching latest NVIDIA driver version from API (OSID {osId})", ex);
                    }
                }

                // Fallback: If API fails, try the processFind page
                var fallbackUrl = $"https://www.nvidia.com/Download/processFind.aspx?psid={psid}&pfid={pfid}&osid=135&lid=1&whql=1&lang=en-us&ctk=0&dltype=1&upCRD={upCRD}";
                
                try
                {
                    var pageResponse = await _httpClient.GetAsync(fallbackUrl);
                    if (pageResponse.IsSuccessStatusCode)
                    {
                        var html = await pageResponse.Content.ReadAsStringAsync();
                        
                        // Find all 3-digit versions and take the highest one that is modern (>= 500)
                        var allMatches = Regex.Matches(html, @"\b(\d{3}\.\d{2})\b");
                        if (allMatches.Count > 0)
                        {
                            var versions = allMatches.Select(m => m.Groups[1].Value)
                                .Distinct()
                                .Where(v => v.StartsWith("5") || v.StartsWith("6")) // Filter for modern versions (500+, 600+)
                                .ToList();
                                
                            var highestVersion = versions
                                .OrderByDescending(v => {
                                    var parts = v.Split('.');
                                    return int.Parse(parts[0]) * 100 + (parts.Length > 1 ? int.Parse(parts[1]) : 0);
                                })
                                .FirstOrDefault();
                            
                            if (highestVersion != null)
                            {
                                await _logger.LogInfoAsync($"Latest NVIDIA driver version (highest scraped): {highestVersion}");
                                return highestVersion;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await _logger.LogErrorAsync("Error scraping NVIDIA download page", ex);
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Error fetching latest NVIDIA driver version", ex);
            }
            
            return null;
        }

        public async Task<bool> InstallDriverAsync(GpuDriverModel driver)
        {
            try
            {
                await _logger.LogInfoAsync($"Starting installation of {driver.Name} ({driver.Id})...");

                // NVIDIA / AMD / Intel GPU drivers have no winget package: manual download is the only
                // honest path, so open the official page instead of running winget with an invented id.
                if (driver.Manufacturer == "NVIDIA" || string.IsNullOrEmpty(driver.Id))
                {
                    await _logger.LogInfoAsync($"{driver.Manufacturer} drivers must be downloaded from the official website. Opening browser...");
                    
                    string url = !string.IsNullOrWhiteSpace(driver.DownloadUrl)
                        ? driver.DownloadUrl
                        : "https://www.nvidia.com/download/index.aspx";
                    Process.Start(new ProcessStartInfo(url)
                    {
                        UseShellExecute = true
                    });
                    
                    return false; // Return false as we didn't actually install
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install -e --id {driver.Id} --silent --force --disable-interactivity --accept-source-agreements --accept-package-agreements",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        await _logger.LogInfoAsync($"Successfully installed {driver.Name}.");
                        _timeline.AddEventKey("event.driverInstalled.title", "event.gpuInstalled.desc", TimelineEventType.Installation, new[] { driver.Name }, new[] { driver.Manufacturer });
                        return true;
                    }
                    else
                    {
                        await _logger.LogErrorAsync($"Failed to install {driver.Name}. Exit Code: {process.ExitCode}", new Exception(output + error));
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Exception during installation of {driver.Name}", ex);
                return false;
            }
        }

        public async Task<bool> UpdateDriverAsync(GpuDriverModel driver)
        {
            try
            {
                await _logger.LogInfoAsync($"Starting update of {driver.Name} ({driver.Id})...");

                // NVIDIA / AMD / Intel GPU drivers have no winget package: manual download is the only
                // honest path, so open the official page instead of running winget with an invented id.
                if (driver.Manufacturer == "NVIDIA" || string.IsNullOrEmpty(driver.Id))
                {
                    await _logger.LogInfoAsync($"{driver.Manufacturer} drivers must be downloaded from the official website. Opening browser...");
                    
                    string url = !string.IsNullOrWhiteSpace(driver.DownloadUrl)
                        ? driver.DownloadUrl
                        : "https://www.nvidia.com/download/index.aspx";
                    Process.Start(new ProcessStartInfo(url)
                    {
                        UseShellExecute = true
                    });
                    
                    return false; // Return false as we didn't actually update
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"upgrade -e --id {driver.Id} --silent --force --disable-interactivity --accept-source-agreements --accept-package-agreements",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        await _logger.LogInfoAsync($"Successfully updated {driver.Name}.");
                        _timeline.AddEventKey("event.gpuUpdated.title", "event.gpuUpdated.desc", TimelineEventType.Installation, new[] { driver.Name }, new[] { driver.LatestVersion ?? string.Empty });
                        return true;
                    }
                    else
                    {
                        await _logger.LogErrorAsync($"Failed to update {driver.Name}. Exit Code: {process.ExitCode}", new Exception(output + error));
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Exception during update of {driver.Name}", ex);
                return false;
            }
        }

    }
}
