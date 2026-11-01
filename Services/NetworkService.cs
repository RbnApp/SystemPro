using System;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using RbnSystemPro.Helpers;

namespace RbnSystemPro.Services
{
    public class NetworkService : INetworkService
    {
        private readonly IAlertService _alertService;
        private readonly ILoggerService _logger;

        public NetworkService(IAlertService alertService, ILoggerService logger)
        {
            _alertService = alertService;
            _logger = logger;
        }

        public async Task<bool> FlushDnsAsync()
        {
            return await RunCommandAsync("ipconfig /flushdns", "Flushing DNS cache...");
        }

        public async Task<bool> ResetNetworkStackAsync()
        {
            string[] commands =
            {
                "netsh winsock reset",
                "netsh int ip reset",
                "ipconfig /release",
                "ipconfig /renew",
                "ipconfig /flushdns"
            };

            bool anySuccess = false;
            foreach (var cmd in commands)
            {
                if (await RunCommandAsync(cmd, "Resetting network stack..."))
                {
                    anySuccess = true;
                }
            }
            return anySuccess;
        }

        public async Task<bool> SetDnsAsync(string primary, string secondary)
        {
            // Değerler PowerShell betiğine metin olarak giriyor: yalnız geçerli IP adresleri kabul edilir.
            if (!System.Net.IPAddress.TryParse(primary, out _) || !System.Net.IPAddress.TryParse(secondary, out _))
            {
                await _logger.LogWarningAsync("DNS ayarı reddedildi: sunucu adresleri geçerli IP değil.");
                return false;
            }

            string script = $@"
                $adapters = Get-NetAdapter | Where-Object {{ $_.Status -eq 'Up' }}
                if (-not $adapters) {{ exit 1 }}
                foreach ($adapter in $adapters) {{
                    Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses ('{primary}', '{secondary}') -ErrorAction Stop
                }}
            ";
            return await RunPowerShellScriptAsync(script, "Updating DNS settings...");
        }

        public async Task<bool> OptimizeTcpStackAsync()
        {
            string[] commands =
            {
                "netsh int tcp set global autotuninglevel=normal",
                "netsh int tcp set global heuristics=disabled",
                "netsh int tcp set global rss=enabled",
                "netsh int tcp set global ecncapability=disabled",
                "netsh int tcp set global timestamps=disabled"
            };

            bool allSuccess = true;
            foreach (var cmd in commands)
            {
                if (!await RunCommandAsync(cmd, "Optimizing TCP stack..."))
                {
                    allSuccess = false;
                }
            }
            return allSuccess;
        }

        public Task<(string Type, string Name)> GetActiveConnectionStatusAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                            ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                            ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                        {
                            string type = ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ? "WiFi" : "LAN";
                            return (type, ni.Name);
                        }
                    }
                    return ("Disconnected", "None");
                }
                catch
                {
                    return ("Unknown", "Error");
                }
            });
        }

        public async Task<System.Collections.Generic.List<string>> GetAvailableWifiNetworksAsync()
        {
            var networks = new System.Collections.Generic.List<string>();
            try
            {
                var processInfo = new ProcessStartInfo("netsh", "wlan show networks")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        // Look for "SSID" followed by any number and a colon (excluding BSSID)
                        if (trimmedLine.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) &&
                            !trimmedLine.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase) &&
                            trimmedLine.Contains(':'))
                        {
                            var parts = trimmedLine.Split(':', 2);
                            if (parts.Length > 1)
                            {
                                string ssid = parts[1].Trim();
                                if (!string.IsNullOrEmpty(ssid) && !networks.Contains(ssid))
                                    networks.Add(ssid);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Failed to list available WiFi networks", ex);
            }
            return networks;
        }

        public async Task<string?> GetCurrentWifiSsidAsync()
        {
            try
            {
                var processInfo = new ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        // Output format: "    SSID                   : MyNetwork" (exclude BSSID)
                        if (trimmedLine.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) &&
                            !trimmedLine.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase) &&
                            trimmedLine.Contains(':'))
                        {
                            var parts = trimmedLine.Split(':', 2);
                            if (parts.Length > 1)
                            {
                                string ssid = parts[1].Trim();
                                if (!string.IsNullOrEmpty(ssid))
                                    return ssid;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Failed to get current WiFi SSID", ex);
            }
            return null;
        }

        public async Task<bool> ConnectToWifiAsync(string ssid, string password, bool autoConnect = true)
        {
            // SSID doğrulanmadan hiçbir süreç çalıştırılmaz (eski kod SSID'yi cmd.exe komut
            // dizesine ekliyordu: x" & calc & " gibi bir ad komut çalıştırabiliyordu).
            if (!WifiProfileHelper.IsValidSsid(ssid))
            {
                await _logger.LogWarningAsync("Wi-Fi bağlantısı reddedildi: SSID geçersiz (1-32 karakter; harf, rakam, boşluk, - ve .).");
                return false;
            }

            string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wifi_{Guid.NewGuid():N}.xml");
            try
            {
                string connectionMode = autoConnect ? "auto" : "manual";
                string escapedSsid = SecurityElement.Escape(ssid) ?? ssid;
                string escapedPassword = SecurityElement.Escape(password) ?? string.Empty;
                bool hasPassword = !string.IsNullOrEmpty(password);

                string securityXml = hasPassword
                    ? $@"<authEncryption>
                <authentication>WPA2PSK</authentication>
                <encryption>AES</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{escapedPassword}</keyMaterial>
            </sharedKey>"
                    : @"<authEncryption>
                <authentication>open</authentication>
                <encryption>none</encryption>
                <useOneX>false</useOneX>
            </authEncryption>";

                string profileXml = $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{escapedSsid}</name>
    <SSIDConfig>
        <SSID>
            <name>{escapedSsid}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>{connectionMode}</connectionMode>
    <MSM>
        <security>
            {securityXml}
        </security>
    </MSM>
</WLANProfile>";

                // netsh DeleteOnClose dosyasını açamıyor (ölçüldü); bu yüzden dosya yazılıp
                // kapatılıyor, komutlar bittikten sonra finally'de siliniyor. Dosya yalnız geçerli
                // kullanıcıya açık (ACL) ve adı GUID ile tahmin edilemez.
                WifiProfileHelper.WriteTemporaryProfile(tempFile, profileXml);

                bool added = await RunCommandAsync(WifiProfileHelper.BuildAddProfileStartInfo(tempFile), "Adding WiFi profile...");

                if (added)
                {
                    return await RunCommandAsync(WifiProfileHelper.BuildConnectStartInfo(ssid), $"Connecting to {ssid}...");
                }
                return false;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Failed to connect to WiFi {ssid}", ex);
                return false;
            }
            finally
            {
                // Şifre diske geçici olarak yazıldı: dosya her durumda (hata/başarı) burada silinir.
                if (!WifiProfileHelper.DeleteTemporaryProfile(tempFile, out string? deleteError))
                {
                    await _logger.LogWarningAsync($"Geçici Wi-Fi profil dosyası silinemedi: {deleteError}");
                }
            }
        }

        public async Task<bool> HasWifiInterfaceAsync()
        {
            return await Task.Run(() =>
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                        return true;
                }
                return false;
            });
        }

        public async Task<string?> GetWifiPasswordAsync(string ssid)
        {
            try
            {
                var processInfo = WifiProfileHelper.BuildShowProfileStartInfo(ssid);

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Key Content", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("Anahtar İçeriği", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(':', 2);
                            if (parts.Length > 1)
                            {
                                return parts[1].Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Failed to retrieve WiFi password for {ssid}", ex);
            }
            return null;
        }

        public async Task<bool> DisconnectWifiAsync()
        {
            return await RunCommandAsync("netsh wlan disconnect", "Disconnecting from WiFi...");
        }

        /// <summary>
        /// Hazır bir <see cref="ProcessStartInfo"/> ile komut çalıştırır. netsh gibi araçlar
        /// bu yolu kullanır: argümanlar ArgumentList ile verilir, kabuk (cmd.exe) devrede değildir.
        /// </summary>
        private async Task<bool> RunCommandAsync(ProcessStartInfo processInfo, string description)
        {
            try
            {
                using var process = Process.Start(processInfo);
                if (process == null) return false;

                // İki akış da yönlendirilmiş: beklemeden ÖNCE eşzamanlı okunmazsa boru dolup kilitlenir.
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
                {
                    await _logger.LogWarningAsync($"Command failed ({description}): {stderr.Trim()}");
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Command failed ({description})", ex);
                return false;
            }
        }

        private async Task<bool> RunCommandAsync(string command, string description)
        {
            try
            {
                var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                if (process == null) return false;

                // Both pipes are redirected: they must be drained CONCURRENTLY with the wait,
                // otherwise a command writing more than the pipe buffer (~4 KB) blocks forever.
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Command failed ({description}): {command}", ex);
                return false;
            }
        }

        private async Task<bool> RunPowerShellScriptAsync(string script, string description)
        {
            try
            {
                var processInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                if (process == null) return false;

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"PowerShell script failed ({description})", ex);
                return false;
            }
        }
    }
}
