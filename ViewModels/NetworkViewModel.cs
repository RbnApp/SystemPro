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
        // DAPP-41: durum metni anahtar + argüman; dil değişince kendiliğinden çevrilir.
        private string _statusKey = "network.status.ready";
        private object?[] _statusArgs = System.Array.Empty<object?>();
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

        public string StatusMessage => Loc.T(_statusKey, _statusArgs);

        private void SetStatus(string key, params object?[] args)
        {
            _statusKey = key;
            _statusArgs = args;
            OnPropertyChanged(nameof(StatusMessage));
        }

        public string ConnectionType
        {
            get => _connectionType;
            set
            {
                SetProperty(ref _connectionType, value);
                OnPropertyChanged(nameof(ConnectionTypeText));
                OnPropertyChanged(nameof(ConnectionSummaryText));
            }
        }

        public string ConnectionName
        {
            get => _connectionName;
            set
            {
                SetProperty(ref _connectionName, value);
                OnPropertyChanged(nameof(ConnectionNameText));
                OnPropertyChanged(nameof(ConnectionSummaryText));
            }
        }

        /// <summary>Bağlantı adı; servis "None"/"Error" döndürdüğünde çevrilir.</summary>
        public string ConnectionNameText => _connectionName switch
        {
            "None" => Loc.T("network.status.none"),
            "Error" => Loc.T("alert.errorTitle"),
            _ => _connectionName
        };

        /// <summary>Bağlantı türü; servis kodları ("WiFi"/"LAN") olduğu gibi, durum sözcükleri çevrilir.</summary>
        public string ConnectionTypeText => _connectionType switch
        {
            "Disconnected" => Loc.T("network.status.disconnected"),
            "Unknown" => Loc.T("common.unknown"),
            "Error" => Loc.T("alert.errorTitle"),
            "Checking..." => Loc.T("network.status.checking"),
            _ => _connectionType
        };

        /// <summary>Rozetteki "tür - ad" satırı (Run bağları XAML'de çöküyordu, tek metin olarak verilir).</summary>
        public string ConnectionSummaryText => $"{ConnectionTypeText} - {ConnectionName}";

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

            FlushDnsCommand = new DelegateCommand(async () => await ExecuteAsync(_networkService.FlushDnsAsync, "network.msg.flushDns"));
            ResetNetworkCommand = new DelegateCommand(async () =>
            {
                // Hard Reset: bağlantı kesilir ve yığın sıfırlanır — onaysız çalışmaz (denetim #13).
                bool confirmed = await _alertService.ShowConfirmAsync(
                    Loc.T("network.confirmResetTitle"),
                    Loc.T("network.confirmResetMsg"));

                if (!confirmed)
                {
                    SetStatus("network.resetCancelled");
                    return;
                }

                await ExecuteAsync(_networkService.ResetNetworkStackAsync, "network.msg.reset");
            });
            OptimizeTcpCommand = new DelegateCommand(async () => await ExecuteAsync(_networkService.OptimizeTcpStackAsync, "network.msg.tcp"));
            SetGoogleDnsCommand = new DelegateCommand(async () => await ExecuteAsync(() => _networkService.SetDnsAsync("8.8.8.8", "8.8.4.4"), "network.msg.googleDns"));
            SetCloudflareDnsCommand = new DelegateCommand(async () => await ExecuteAsync(() => _networkService.SetDnsAsync("1.1.1.1", "1.0.0.1"), "network.msg.cloudflareDns"));
            RefreshNetworksCommand = new DelegateCommand(async () => await RefreshStatusAsync());
            ConnectToWifiCommand = new DelegateCommand<string>(async (ssid) => {
                if (!string.IsNullOrEmpty(ssid)) 
                {
                    bool success = await ExecuteAsync(() => _networkService.ConnectToWifiAsync(ssid, ""), "network.msg.connecting", args: new object?[] { ssid });
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
                        "network.msg.connecting",
                        "network.msg.connectFailed",
                        new object?[] { ssid }
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
                CurrentWifiPassword = pass ?? Loc.T("network.msg.noPassword");
                IsPasswordInfoModalVisible = true;
            });

            DisconnectCommand = new DelegateCommand(async () => {
                await ExecuteAsync(_networkService.DisconnectWifiAsync, "network.msg.disconnected");
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

        /// <summary>
        /// DAPP-41: mesajlar ANAHTAR olarak alınır; ekrana ve etkinlik kaydına çevrilerek yazılır.
        /// </summary>
        private async Task<bool> ExecuteAsync(System.Func<Task<bool>> action, string successKey, string? failureKey = null, object?[]? args = null)
        {
            if (IsBusy) return false;
            IsBusy = true;
            SetStatus("network.status.processing");

            bool success = await action();

            IsBusy = false;
            SetStatus(success ? "network.status.success" : "network.status.failed");

            if (success)
            {
                var msg = Loc.T(successKey, args ?? System.Array.Empty<object?>());
                _timelineService.AddEvent(Loc.T("network.title"), msg, RbnSystemPro.Models.TimelineEventType.Success);
                await _alertService.ShowSuccessAsync(Loc.T("alert.successTitle"), msg);
            }
            else
            {
                var msg = Loc.T(failureKey ?? "network.msg.actionFailed", args ?? System.Array.Empty<object?>());
                _timelineService.AddEvent(Loc.T("network.title"), msg, RbnSystemPro.Models.TimelineEventType.Error);
                await _alertService.ShowErrorAsync(Loc.T("alert.errorTitle"), msg);
            }
            return success;
        }
    }
}
