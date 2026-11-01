using System.Collections.Generic;
using System.Threading.Tasks;
using RbnSystemPro.Models;

namespace RbnSystemPro.Services
{
    public interface ISoftwareService
    {
        List<ProgramModel> GetAvailablePrograms();
        Task CheckInstalledProgramsAsync(List<ProgramModel> programs);
        Task<bool> InstallProgramAsync(ProgramModel program);
        Task<bool> UninstallProgramAsync(ProgramModel program);
        Task<bool> IsWingetAvailableAsync();
        Task<bool> EnableWingetAsync();
    }

    public class SoftwareService : ISoftwareService
    {
        private readonly ILoggerService _logger;
        private readonly ITimelineService _timeline;

        public SoftwareService(ILoggerService logger, ITimelineService timeline)
        {
            _logger = logger;
            _timeline = timeline;
        }

        public List<ProgramModel> GetAvailablePrograms()
        {
            // Initial curated list of essential software
            // IDs are from winget repository
            return new List<ProgramModel>
            {
                // Web Browsers
                new() { Name = "Google Chrome", Id = "Google.Chrome", Description = "Fast and secure web browser.", IconSymbol = "Globe24", HexColor = "#4285F4", Category = "Web" },
                new() { Name = "Mozilla Firefox", Id = "Mozilla.Firefox", Description = "Privacy-focused web browser.", IconSymbol = "Globe24", HexColor = "#FF9500", Category = "Web" },
                new() { Name = "Brave", Id = "Brave.Brave", Description = "Secure browser with built-in ad blocker.", IconSymbol = "Globe24", HexColor = "#FB542B", Category = "Web" },
                new() { Name = "Opera", Id = "Opera.Opera", Description = "Feature-rich browser with VPN.", IconSymbol = "Globe24", HexColor = "#FF1B2D", Category = "Web" },
                
                // Utilities
                new() { Name = "WinRAR", Id = "RARLab.WinRAR", Description = "Powerful archive manager.", IconSymbol = "FolderZip24", HexColor = "#7B68EE", Category = "Utilities" },
                new() { Name = "7-Zip", Id = "7zip.7zip", Description = "Open source file archiver.", IconSymbol = "FolderZip24", HexColor = "#32CD32", Category = "Utilities" },
                new() { Name = "Lightshot", Id = "Skillbrains.Lightshot", Description = "Fast screenshot tool.", IconSymbol = "Screenshot24", HexColor = "#A1359D", Category = "Utilities" },
                
                // Test & Benchmark
                new() { Name = "CrystalDiskInfo", Id = "CrystalDewWorld.CrystalDiskInfo", Description = "HDD/SSD health monitoring tool.", IconSymbol = "HardDrive24", HexColor = "#00CED1", Category = "Test" },
                new() { Name = "Hard Disk Sentinel", Id = "JanosMathe.HardDiskSentinel.Standard", Description = "Advanced HDD/SSD monitoring and diagnostics.", IconSymbol = "HardDrive24", HexColor = "#FF6347", Category = "Test" },
                new() { Name = "FurMark", Id = "Geeks3D.FurMark.2", Description = "GPU stress test and benchmark.", IconSymbol = "DeveloperBoard24", HexColor = "#DC143C", Category = "Test" },
                
                // Media
                new() { Name = "VLC Media Player", Id = "VideoLAN.VLC", Description = "Universal media player.", IconSymbol = "MoviesAndTv24", HexColor = "#FF8C00", Category = "Media" },
                new() { Name = "K-Lite Codec Pack", Id = "CodecGuide.K-LiteCodecPack.Full", Description = "Complete codec pack for media playback.", IconSymbol = "Video24", HexColor = "#0066CC", Category = "Media" },
                
                // Development
                new() { Name = "Notepad++", Id = "Notepad++.Notepad++", Description = "Advanced text editor.", IconSymbol = "DocumentEdit24", HexColor = "#90EE90", Category = "Development" },

                // Gaming
                new() { Name = "Discord", Id = "Discord.Discord", Description = "Voice and text chat for gamers.", IconSymbol = "People24", HexColor = "#5865F2", Category = "Gaming" },
                new() { Name = "Steam", Id = "Valve.Steam", Description = "Ultimate destination for playing games.", IconSymbol = "Games24", HexColor = "#171A21", Category = "Gaming" },
                new() { Name = "Epic Games", Id = "EpicGames.EpicGamesLauncher", Description = "Store for PC games and mods.", IconSymbol = "Games24", HexColor = "#313131", Category = "Gaming" },
            };
        }

        public async Task CheckInstalledProgramsAsync(List<ProgramModel> programs)
        {
             await Task.Run(() => 
             {
                 try
                 {
                    // Basic Registry check for installed software (Fastest method)
                    // Combining HKCU and HKLM views
                    var installed = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    
                    void CheckKey(Microsoft.Win32.RegistryKey root)
                    {
                        using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                        if (key != null)
                        {
                            foreach (var subkeyName in key.GetSubKeyNames())
                            {
                                using var subkey = key.OpenSubKey(subkeyName);
                                var name = subkey?.GetValue("DisplayName")?.ToString();
                                if (!string.IsNullOrEmpty(name)) installed.Add(name);
                            }
                        }
                    }

                    CheckKey(Microsoft.Win32.Registry.LocalMachine);
                    CheckKey(Microsoft.Win32.Registry.CurrentUser);
                    // Also check for 32-bit on 64-bit OS
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (key != null)
                        {
                            foreach (var subkeyName in key.GetSubKeyNames())
                            {
                                using var subkey = key.OpenSubKey(subkeyName);
                                var name = subkey?.GetValue("DisplayName")?.ToString();
                                if (!string.IsNullOrEmpty(name)) installed.Add(name);
                            }
                        }
                    }

                    foreach (var prog in programs)
                    {
                        // Loose matching logic: If registry name contains our app name
                        bool exists = installed.Any(i => i.Contains(prog.Name, System.StringComparison.OrdinalIgnoreCase));
                        
                        // Exceptions / Specific checks
                        if (prog.Id == "Google.Chrome" && installed.Any(i => i.Contains("Google Chrome"))) exists = true;
                        if (prog.Id == "Mozilla.Firefox" && installed.Any(i => i.Contains("Firefox"))) exists = true;

                        if (exists)
                        {
                            prog.IsInstalled = true;
                            prog.Status = "Already Installed";
                            prog.IsSelected = false; // Disable selection by default
                        }
                    }
                 }
                 catch (Exception ex)
                 {
                     _ = _logger.LogWarningAsync($"Kurulu program listesi okunamadı (registry): {ex.Message}");
                 }
             });
        }

        public async Task<bool> IsWingetAvailableAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "winget",
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = System.Diagnostics.Process.Start(processStartInfo);
                    process?.WaitForExit();
                    return process?.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"winget sürümü okunamadı: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> EnableWingetAsync()
        {
            try
            {
                await _logger.LogInfoAsync("Attempting to enable/install Winget via PowerShell...");

                // This PowerShell command attempts to register the App Installer package which contains Winget
                var psCommand = "Get-AppxPackage -AllUsers -Name Microsoft.DesktopAppInstaller | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\"}";
                
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        await _logger.LogInfoAsync("Winget registration command completed successfully.");
                        // Verify if it's actually available now
                        return await IsWingetAvailableAsync();
                    }
                    else
                    {
                        await _logger.LogErrorAsync($"Failed to enable Winget. Exit Code: {process.ExitCode}", new System.Exception(output + error));
                        return false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                await _logger.LogErrorAsync("Exception while enabling Winget", ex);
                return false;
            }
        }

        public async Task<bool> InstallProgramAsync(ProgramModel program)
        {
            try
            {
                if (!await IsWingetAvailableAsync())
                {
                    await _logger.LogErrorAsync($"Winget is not installed on this system. Cannot install {program.Name}.");
                    return false;
                }

                await _logger.LogInfoAsync($"Starting installation of {program.Name} ({program.Id})...");

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install -e --id {program.Id} --silent --accept-package-agreements --accept-source-agreements",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    
                    // We can read output if we want to update progress detail, 
                    // but for now we just wait for exit to keep it simple.
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        await _logger.LogInfoAsync($"Successfully installed {program.Name}.");
                        _timeline.AddEvent($"{program.Name} Installed", $"{program.Name} has been successfully installed via Winget.", TimelineEventType.Installation);
                        return true;
                    }
                    else
                    {
                        await _logger.LogErrorAsync($"Failed to install {program.Name}. Exit Code: {process.ExitCode}", new System.Exception(output + error));
                        return false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                await _logger.LogErrorAsync($"Exception during installation of {program.Name}", ex);
                return false;
            }
        }

        public async Task<bool> UninstallProgramAsync(ProgramModel program)
        {
            try
            {
                await _logger.LogInfoAsync($"Starting uninstallation of {program.Name} ({program.Id})...");

                // Step 1: Winget uninstall (completely silent, no user interaction)
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"uninstall -e --id {program.Id} --silent --force --disable-interactivity --accept-source-agreements",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        await _logger.LogErrorAsync($"Winget uninstall failed for {program.Name}. Exit Code: {process.ExitCode}", new System.Exception(output + error));
                        return false;
                    }
                }

                await _logger.LogInfoAsync($"Winget uninstall completed for {program.Name}. Starting deep cleanup...");

                // Step 2: Kayıt defteri temizliği — YALNIZ tam eşleşen kaldırma kaydı.
                // Klasör silme kaldırıldı (denetim #3): ada göre klasör/anahtar silmek ilgisiz
                // veriyi götürebiliyordu. Ayrıntı: UninstallCleanup.
                int removedKeys = await Task.Run(() =>
                    UninstallCleanup.CleanUninstallRegistry(program.Id, program.Name, _logger));

                if (removedKeys > 0)
                {
                    await _logger.LogInfoAsync($"Kaldırma kaydı temizlendi ({removedKeys} anahtar): {program.Name}");
                }

                await _logger.LogInfoAsync($"Successfully uninstalled and cleaned {program.Name}.");
                _timeline.AddEvent($"{program.Name} Uninstalled", $"{program.Name} kaldırıldı; eşleşen kaldırma kaydı temizlendi ({removedKeys} anahtar).", TimelineEventType.Success);
                return true;
            }
            catch (System.Exception ex)
            {
                await _logger.LogErrorAsync($"Exception during uninstallation of {program.Name}", ex);
                return false;
            }
        }

    }
}