using System.Threading.Tasks;

namespace RbnSystemPro.Services
{
    public interface INetworkService
    {
        Task<bool> FlushDnsAsync();
        Task<bool> ResetNetworkStackAsync();
        Task<bool> SetDnsAsync(string primary, string secondary);
        Task<bool> OptimizeTcpStackAsync();
        Task<(string Type, string Name)> GetActiveConnectionStatusAsync();
        Task<System.Collections.Generic.List<string>> GetAvailableWifiNetworksAsync();
        Task<bool> ConnectToWifiAsync(string ssid, string password, bool autoConnect = true);
        Task<bool> HasWifiInterfaceAsync();
        Task<string?> GetWifiPasswordAsync(string ssid);
        Task<string?> GetCurrentWifiSsidAsync();
        Task<bool> DisconnectWifiAsync();
    }
}
