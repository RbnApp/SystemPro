using System.Threading.Tasks;
using System.Windows.Input;
using RbnSystemPro.Services;
using RbnSystemPro.Helpers;

namespace RbnSystemPro.ViewModels
{
    public class NetworkViewModel : ObservableObject
    {
        private readonly INetworkService _networkService;
        private readonly IAlertService _alertService;
        private readonly ITimelineService _timelineService;
        private bool _isBusy;
        private string _statusMessage = "Ready";
        private string _connectionType = "Checking...";
        private string _connectionName = "";
        private System.Collections.ObjectModel.ObservableCollection<string> _wifiNetworks = new();
        private string? _connectedWifiSsid;
        private string _selectedSsid = "";
        private bool _isModalVisible;
        private bool _autoConnect = true;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ConnectionType
        {
            get => _connectionType;
            set => SetProperty(ref _connectionType, value);
        }

        public string ConnectionName
        {
            get => _connectionName;
            set => SetProperty(ref _connectionName, value);
        }

        public System.Collections.ObjectModel.ObservableCollection<string> WifiNetworks
        {
            get => _wifiNetworks;
            set => SetProperty(ref _wifiNetworks, value);
        }

        public string SelectedSsid
        {
            get => _selectedSsid;
            set => SetProperty(ref _selectedSsid, value);
        }

        public string? ConnectedWifiSsid
        {
            get => _connectedWifiSsid;
            set 
            {
                SetProperty(ref _connectedWifiSsid, value);
                OnPropertyChanged(nameof(IsWifiConnected));
            }
        }

        public bool IsWifiConnected => !string.IsNullOrEmpty(ConnectedWifiSsid);

        public bool IsModalVisible
        {
            get => _isModalVisible;
            set => SetProperty(ref _isModalVisible, value);
        }

        public bool AutoConnect
        {
            get => _autoConnect;
            set => SetProperty(ref _autoConnect, value);
        }

        private bool _isPasswordInfoModalVisible;
        public bool IsPasswordInfoModalVisible
        {
            get => _isPasswordInfoModalVisible;
            set => SetProperty(ref _isPasswordInfoModalVisible, value);
        }

        private string _currentWifiPassword = "";
        public string CurrentWifiPassword
        {
            get => _currentWifiPassword;
            set => SetProperty(ref _currentWifiPassword, value);
        }

        public ICommand FlushDnsCommand { get; }
        public ICommand ResetNetworkCommand { get; }
        public ICommand OptimizeTcpCommand { get; }
        public ICommand SetGoogleDnsCommand { get; }
        public ICommand SetCloudflareDnsCommand { get; }
        public ICommand RefreshNetworksCommand { get; }
        public ICommand ConnectToWifiCommand { get; }
        public ICommand ShowConnectionModalCommand { get; }
        public ICommand CancelConnectionCommand { get; }
        public ICommand ConnectWithPasswordCommand { get; }
        public ICommand ShowWifiPasswordCommand { get; }
        public ICommand ClosePasswordInfoCommand { get; }

        public NetworkViewModel(INetworkService networkService, IAlertService alertService, ITimelineService timelineService)
        {
            _networkService = networkService;
            _alertService = alertService;
            _timelineService = timelineService;

            FlushDnsCommand = new DelegateCommand(async () => await ExecuteAsync(_networkService.FlushDnsAsync, "DNS cache flushed."));
            ResetNetworkCommand = new DelegateCommand(async () =>
            {
                // Hard Reset: bağlantı kesilir ve yığın sıfırlanır — onaysız çalışmaz (denetim #13).
                bool confirmed = await _alertService.ShowConfirmAsync(
                    "Ağ Yığınını Sıfırla",
                    "Winsock ve TCP/IP sıfırlanacak, IP adresi yenilenecek. İnternet bağlantısı birkaç " +
                    "saniye kesilecek ve yeniden başlatma önerilir.\n\nDevam edilsin mi?");

                if (!confirmed)
                {
                    StatusMessage = "Sıfırlama iptal edildi.";
                    return;
                }

                await ExecuteAsync(_networkService.ResetNetworkStackAsync, "Network stack reset. System restart recommended.");
            });
            OptimizeTcpCommand = new DelegateCommand(async () => await ExecuteAsync(_networkService.OptimizeTcpStackAsync, "TCP stack optimized."));
            SetGoogleDnsCommand = new DelegateCommand(async () => await ExecuteAsync(() => _networkService.SetDnsAsync("8.8.8.8", "8.8.4.4"), "Google DNS applied."));
            SetCloudflareDnsCommand = new DelegateCommand(async () => await ExecuteAsync(() => _networkService.SetDnsAsync("1.1.1.1", "1.0.0.1"), "Cloudflare DNS applied."));
            RefreshNetworksCommand = new DelegateCommand(async () => await RefreshStatusAsync());
            ConnectToWifiCommand = new DelegateCommand<string>(async (ssid) => {
                if (!string.IsNullOrEmpty(ssid)) 
                {
                    bool success = await ExecuteAsync(() => _networkService.ConnectToWifiAsync(ssid, ""), $"Attempting to connect to {ssid}...");
                    if (success)
                    {
                        await Task.Delay(4000);
                        await RefreshStatusAsync();
                    }
                }
            });

            ShowConnectionModalCommand = new DelegateCommand<string>((ssid) => {
                if (string.IsNullOrEmpty(ssid)) return;
                SelectedSsid = ssid;
                IsModalVisible = true;
            });

            CancelConnectionCommand = new DelegateCommand(() => {
                IsModalVisible = false;
                SelectedSsid = "";
            });

            ConnectWithPasswordCommand = new DelegateCommand<string>(async (password) => {
                string ssid = SelectedSsid;
                bool auto = AutoConnect;
                IsModalVisible = false;
                SelectedSsid = "";

                if (!string.IsNullOrEmpty(ssid))
                {
                    bool success = await ExecuteAsync(
                        () => _networkService.ConnectToWifiAsync(ssid, password, auto), 
                        $"Attempting to connect to {ssid}...",
                        "Connection failed. Please verify the password and try again."
                    );

                    if (success)
                    {
                        // Wait a moment for connection to establish and DHCP to assign IP
                        await Task.Delay(4000); 
                        await RefreshStatusAsync();
                    }
                }
            });

            ShowWifiPasswordCommand = new DelegateCommand(async () => {
                if (string.IsNullOrEmpty(ConnectedWifiSsid)) return;
                
                string? pass = await _networkService.GetWifiPasswordAsync(ConnectedWifiSsid);
                CurrentWifiPassword = pass ?? "Could not retrieve password.";
                IsPasswordInfoModalVisible = true;
            });

            DisconnectCommand = new DelegateCommand(async () => {
                await ExecuteAsync(_networkService.DisconnectWifiAsync, "Disconnected from WiFi.");
                await RefreshStatusAsync();
            });

            ClosePasswordInfoCommand = new DelegateCommand(() => {
                IsPasswordInfoModalVisible = false;
                CurrentWifiPassword = "";
            });

            _ = RefreshStatusAsync();
        }

        public ICommand DisconnectCommand { get; }

        public async Task RefreshStatusAsync()
        {
            var status = await _networkService.GetActiveConnectionStatusAsync();
            ConnectionType = status.Type;
            ConnectionName = status.Name;

            ConnectedWifiSsid = await _networkService.GetCurrentWifiSsidAsync();

            bool hasWifi = await _networkService.HasWifiInterfaceAsync();
            if (hasWifi)
            {
                var networks = await _networkService.GetAvailableWifiNetworksAsync();
                WifiNetworks.Clear();
                foreach (var net in networks)
                {
                    WifiNetworks.Add(net);
                }
            }
            else
            {
                WifiNetworks.Clear();
            }
        }

        private async Task<bool> ExecuteAsync(System.Func<Task<bool>> action, string successMessage, string? failureMessage = null)
        {
            if (IsBusy) return false;
            IsBusy = true;
            StatusMessage = "Processing...";

            bool success = await action();

            IsBusy = false;
            StatusMessage = success ? "Success" : "Failed";

            if (success)
            {
                _timelineService.AddEvent("Network Optimizer", successMessage, RbnSystemPro.Models.TimelineEventType.Success);
                await _alertService.ShowSuccessAsync("Success", successMessage);
            }
            else
            {
                string msg = failureMessage ?? "Action failed. Please run as administrator.";
                _timelineService.AddEvent("Network Optimizer", msg, RbnSystemPro.Models.TimelineEventType.Error);
                await _alertService.ShowErrorAsync("Error", msg);
            }
            return success;
        }
    }
}
