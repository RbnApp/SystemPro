using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using RbnSystemPro.Models;
using RbnSystemPro.Helpers;

namespace RbnSystemPro.Services
{
    public interface ICleanerService
    {
        Task<List<BrowserModel>> GetInstalledBrowsersAsync();
        Task<bool> CleanBrowserAsync(BrowserModel browser);
        Task<int> ScanRegistryAsync();
        Task<int> CleanRegistryAsync();
        Task<long> GetTempFileSizeAsync();
        Task<bool> CleanTempFilesAsync();
    }

    public class BrowserModel : ObservableObject
    {
        private string _name = string.Empty;
        private string _path = string.Empty;
        private bool _isSelected;
        private string _statusKey = "status.ready";

        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public string Path { get => _path; set => SetProperty(ref _path, value); }
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
        // DAPP-41: çevrilebilir durum metni anahtar olarak tutulur.
        public string StatusKey { get => _statusKey; set => SetProperty(ref _statusKey, value); }
        public string Icon { get; set; } = string.Empty;
    }

    public class CleanerService : ICleanerService
    {
        private readonly ILoggerService _logger;
        private readonly ITimelineService _timeline;

        public CleanerService(ILoggerService logger, ITimelineService timeline)
        {
            _logger = logger;
            _timeline = timeline;
        }

        public async Task<List<BrowserModel>> GetInstalledBrowsersAsync()
        {
            return await Task.Run(() =>
            {
                var browsers = new List<BrowserModel>();
                
                // Chrome
                if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data")))
                    browsers.Add(new BrowserModel { Name = "Google Chrome", Icon = "Globe24", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data"), IsSelected = true });

                // Brave
                if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BraveSoftware\Brave-Browser\User Data")))
                    browsers.Add(new BrowserModel { Name = "Brave Browser", Icon = "Globe24", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BraveSoftware\Brave-Browser\User Data"), IsSelected = true });

                // Edge
                if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data")))
                    browsers.Add(new BrowserModel { Name = "Microsoft Edge", Icon = "Globe24", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data"), IsSelected = true });

                // Firefox
                if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox\Profiles")))
                    browsers.Add(new BrowserModel { Name = "Mozilla Firefox", Icon = "Globe24", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox\Profiles"), IsSelected = true });

                // Opera Stable
                var operaPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Opera Software\Opera Stable");
                if (Directory.Exists(operaPath))
                    browsers.Add(new BrowserModel { Name = "Opera", Icon = "Globe24", Path = operaPath, IsSelected = true });

                // Opera GX
                var operaGxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Opera Software\Opera GX Stable");
                if (Directory.Exists(operaGxPath))
                    browsers.Add(new BrowserModel { Name = "Opera GX", Icon = "Globe24", Path = operaGxPath, IsSelected = true });

                return browsers;
            });
        }

        public async Task<bool> CleanBrowserAsync(BrowserModel browser)
        {
            try
            {
                await _logger.LogInfoAsync($"Cleaning {browser.Name}...");
                
                if (browser.Name.Contains("Chrome") || browser.Name.Contains("Brave") || browser.Name.Contains("Edge") || browser.Name.Contains("Opera"))
                {
                    // Chromium browsers
                    string[] subfolders = { "Default/Cache", "Default/Code Cache", "Default/GPUCache", "ShaderCache", "Cache", "System Cache", "Code Cache" };
                    foreach (var folder in subfolders)
                    {
                        var fullPath = Path.Combine(browser.Path, folder);
                        if (Directory.Exists(fullPath)) CleanDirectory(fullPath);
                    }
                }
                else if (browser.Name.Contains("Firefox"))
                {
                    // Firefox
                    if (Directory.Exists(browser.Path))
                    {
                        var profiles = Directory.GetDirectories(browser.Path);
                        foreach (var profile in profiles)
                        {
                            var cachePath = Path.Combine(profile, "cache2");
                            if (Directory.Exists(cachePath)) CleanDirectory(cachePath);
                        }
                    }
                }

                _timeline.AddEventKey("event.browserCleaned.title", "event.browserCleaned.desc", TimelineEventType.Success, new[] { browser.Name });
                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Failed to clean {browser.Name}", ex);
                _timeline.AddEvent(Loc.T("event.browserCleanFailed.title", browser.Name), ex.Message, TimelineEventType.Error);
                return false;
            }
        }

        public async Task<int> ScanRegistryAsync()
        {
            return await Task.Run(() =>
            {
                int itemsFound = 0;
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache", false))
                    {
                        if (key != null)
                        {
                            foreach (var valName in key.GetValueNames())
                            {
                                if (!File.Exists(valName) && valName.Contains("\\"))
                                {
                                    itemsFound++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"Registry taranamadı (MuiCache): {ex.Message}");
                }
                return itemsFound;
            });
        }

        public async Task<int> CleanRegistryAsync()
        {
            return await Task.Run(() =>
            {
                int itemsCleaned = 0;
                // Basic cleanup: removing obsolete MUICache entries
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache", true))
                    {
                        if (key != null)
                        {
                            foreach (var valName in key.GetValueNames())
                            {
                                if (!File.Exists(valName) && valName.Contains("\\"))
                                {
                                    key.DeleteValue(valName, false);
                                    itemsCleaned++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"Registry temizlenemedi (MuiCache): {ex.Message}");
                }
                if (itemsCleaned > 0) _timeline.AddEventKey("event.registryClean.title", "event.registryClean.desc", TimelineEventType.Success, null, new[] { itemsCleaned.ToString() });
                return itemsCleaned;
            });
        }

        private static IEnumerable<string> GetDistinctTempPaths()
        {
            string[] rawPaths =
            {
                Path.GetTempPath(),
                @"C:\Windows\Temp",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")
            };

            return rawPaths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<long> GetTempFileSizeAsync()
        {
            return await Task.Run(() =>
            {
                long size = 0;
                foreach (var path in GetDistinctTempPaths())
                {
                    if (Directory.Exists(path))
                    {
                        try { size += GetDirectorySize(path); }
                        catch (Exception ex)
                        {
                            _ = _logger.LogWarningAsync($"Geçici klasör boyutu ölçülemedi ({path}): {ex.Message}");
                        }
                    }
                }
                return size;
            });
        }

        public async Task<bool> CleanTempFilesAsync()
        {
            return await Task.Run(() =>
            {
                foreach (var path in GetDistinctTempPaths())
                {
                    if (Directory.Exists(path)) CleanDirectory(path);
                }
                _timeline.AddEventKey("event.systemClean.title", "event.systemClean.desc", TimelineEventType.Success);
                return true;
            });
        }

        private void CleanDirectory(string path)
        {
            try
            {
                var dir = new DirectoryInfo(path);
                int failedItems = 0;
                foreach (var file in dir.GetFiles())
                {
                    try { file.Delete(); }
                    catch { failedItems++; }
                }
                foreach (var subDir in dir.GetDirectories())
                {
                    try { subDir.Delete(true); }
                    catch { failedItems++; }
                }
                // Tek tek günlüğe yazmak binlerce satır üretirdi: klasör başına tek özet.
                if (failedItems > 0)
                    _ = _logger.LogWarningAsync($"{path} içinde {failedItems} öğe silinemedi (kullanımda veya yetki yok).");
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Geçici klasör taranamadı ({path}): {ex.Message}");
            }
        }

        private long GetDirectorySize(string path)
        {
            var dir = new DirectoryInfo(path);
            long size = 0;
            int unreadable = 0;
            foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
            {
                try { size += file.Length; }
                catch { unreadable++; }
            }
            if (unreadable > 0)
                _ = _logger.LogWarningAsync($"{path} içinde {unreadable} dosyanın boyutu okunamadı (hesap eksik olabilir).");
            return size;
        }
    }
}
