using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using RbnSystemPro.Helpers;

namespace RbnSystemPro.Services
{
    public interface IUpdateService
    {
        Task<(bool HasUpdate, string Version, string DownloadUrl)> CheckForUpdatesAsync();
        string CurrentVersion { get; }
    }

    public class UpdateService : IUpdateService
    {
        // AppVersion "v" öneki taşımaz; eski/yeni biçim okunsa da baştaki "v" temizlenir.
        public string CurrentVersion => GlobalConfig.AppVersion.TrimStart('v', 'V');
        public async Task<(bool HasUpdate, string Version, string DownloadUrl)> CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "RbnSystemPro-UpdateChecker");
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.GetStringAsync(GlobalConfig.VersionUrl);
                var data = JsonSerializer.Deserialize<UpdateInfo>(response);

                if (data != null && IsNewer(data.version, CurrentVersion))
                {
                    return (true, data.version, data.url);
                }

                return (false, CurrentVersion, "");
            }
            catch
            {
                return (false, CurrentVersion, "");
            }
        }

        private bool IsNewer(string latest, string current)
        {
            if (Version.TryParse(latest, out var vLatest) && Version.TryParse(current, out var vCurrent))
            {
                return vLatest > vCurrent;
            }
            return false;
        }
    }

    public class UpdateInfo
    {
        public string version { get; set; } = "";
        public string url { get; set; } = "";
    }
}
