using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Win32;
using RbnSystemPro.Models;

namespace RbnSystemPro.Services
{
    public interface ISystemService
    {
        Task<SystemHealthModel> GetSystemHealthAsync();
        Task LaunchWindowsUpdateAsync();
    }

    public class SystemService : ISystemService
    {
        private readonly ILoggerService _logger;

        public SystemService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task<SystemHealthModel> GetSystemHealthAsync()
        {
            var health = new SystemHealthModel();

            try
            {
                await Task.Run(() =>
                {
                    // 1. Check Defender / Antivirus Status via SecurityCenter2 + Defender fallback
                    health.IsDefenderActive = CheckSecurityProduct("AntiVirusProduct") || CheckWindowsDefenderActive();
                    
                    // 2. Check Firewall Status via SecurityCenter2 + Windows Firewall Registry
                    health.IsFirewallActive = CheckSecurityProduct("FirewallProduct") || CheckWindowsFirewallActive();

                    // 3. Check UAC
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                        if (key != null)
                        {
                            var uacValue = key.GetValue("EnableLUA");
                            health.IsUacEnabled = uacValue is int intVal && intVal == 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = _logger.LogWarningAsync($"UAC durumu okunamadı (registry ilkesi): {ex.Message}");
                    }

                    // 4. Check Secure Boot (Registry)
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                        if (key != null)
                        {
                            var sbValue = key.GetValue("UEFISecureBootEnabled");
                            health.IsSecureBootEnabled = sbValue is int intVal && intVal == 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = _logger.LogWarningAsync($"Secure Boot durumu okunamadı (registry SecureBoot durumu): {ex.Message}");
                    }

                    // 5. Check TPM Status
                    health.IsTpmEnabled = CheckTpmEnabled();

                    // 6. Check Windows Update Last Success
                    health.LastUpdateCheck = GetLastWindowsUpdateDate();
                });
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Error gathering system health data", ex);
            }

            return health;
        }

        private bool CheckSecurityProduct(string className)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", $"SELECT * FROM {className}");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var stateObj = obj["productState"];
                    if (stateObj != null && int.TryParse(stateObj.ToString(), out int state))
                    {
                        // Bit 12 (0x1000) in SecurityCenter2 productState indicates the product is enabled/active
                        if ((state & 0x1000) != 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Güvenlik ürünü durumu okunamadı (SecurityCenter2 / {className}): {ex.Message}");
            }
            return false;
        }

        private bool CheckWindowsDefenderActive()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender", "SELECT AntivirusEnabled, RealTimeProtectionEnabled FROM MSFT_MpComputerStatus");
                foreach (ManagementObject obj in searcher.Get())
                {
                    bool avEnabled = obj["AntivirusEnabled"] is bool b1 && b1;
                    bool rtpEnabled = obj["RealTimeProtectionEnabled"] is bool b2 && b2;
                    if (avEnabled || rtpEnabled) return true;
                }
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Defender durumu okunamadı (MSFT_MpComputerStatus): {ex.Message}");
            }

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender");
                if (key != null)
                {
                    var disableVal = key.GetValue("DisableAntiSpyware");
                    if (disableVal is int d && d == 1) return false;
                }
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Defender registry kontrolü başarısız: {ex.Message}");
            }

            return false;
        }

        private bool CheckWindowsFirewallActive()
        {
            try
            {
                string[] profiles = { "StandardProfile", "PublicProfile", "DomainProfile" };
                foreach (var profile in profiles)
                {
                    using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\{profile}");
                    if (key != null && key.GetValue("EnableFirewall") is int val && val == 1)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Güvenlik duvarı durumu okunamadı (FirewallPolicy registry): {ex.Message}");
            }
            return false;
        }

        private bool CheckTpmEnabled()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftTpm", "SELECT IsEnabled_InitialValue, IsActivated_InitialValue FROM Win32_Tpm");
                foreach (ManagementObject obj in searcher.Get())
                {
                    bool enabled = obj["IsEnabled_InitialValue"] is bool b1 && b1;
                    bool activated = obj["IsActivated_InitialValue"] is bool b2 && b2;
                    if (enabled || activated) return true;
                }
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"TPM durumu okunamadı (Win32_Tpm): {ex.Message}");
            }

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\TPM");
                if (key != null && key.GetValue("Start") is int startVal && (startVal == 2 || startVal == 3))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"TPM registry kontrolü başarısız: {ex.Message}");
            }

            return false;
        }

        private string GetLastWindowsUpdateDate()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install");
                var lastSuccess = key?.GetValue("LastSuccessTime")?.ToString();
                if (!string.IsNullOrWhiteSpace(lastSuccess))
                {
                    return lastSuccess;
                }
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Son Windows Update tarihi okunamadı (registry): {ex.Message}");
            }

            try
            {
                DateTime latestDate = DateTime.MinValue;
                string? latestRaw = null;
                using var searcher = new ManagementObjectSearcher("SELECT InstalledOn FROM Win32_QuickFixEngineering");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var installedOn = obj["InstalledOn"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(installedOn))
                    {
                        if (DateTime.TryParse(installedOn, out var parsedDate))
                        {
                            if (parsedDate > latestDate)
                            {
                                latestDate = parsedDate;
                                latestRaw = parsedDate.ToString("yyyy-MM-dd");
                            }
                        }
                        else if (latestRaw == null)
                        {
                            latestRaw = installedOn;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(latestRaw))
                {
                    return latestRaw;
                }
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Son yama tarihi okunamadı (Win32_QuickFixEngineering): {ex.Message}");
            }

            return "Up to Date";
        }

        public Task LaunchWindowsUpdateAsync()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Windows Update ayarları açılamadı: {ex.Message}");
            }
            return Task.CompletedTask;
        }
    }
}
