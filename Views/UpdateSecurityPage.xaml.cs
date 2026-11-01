using System.Windows;
using System.Windows.Controls;
using RbnSystemPro.Services;
using RbnSystemPro.Models;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RbnSystemPro.Views
{
    public partial class UpdateSecurityPage : Page, INotifyPropertyChanged
    {
        private readonly ISystemService _systemService;
        private SystemHealthModel _health = new();

        public SystemHealthModel Health
        {
            get => _health;
            set { _health = value; OnPropertyChanged(); }
        }

        // Helper properties for display
        public string DefenderStatusText => Loc.T(_health.IsDefenderActive ? "security.defender.active" : "security.defender.inactive");
        public string FirewallStatusText => Loc.T(_health.IsFirewallActive ? "security.firewall.active" : "security.firewall.inactive");
        public string SecureBootStatusText => Loc.T(_health.IsSecureBootEnabled ? "security.secureBoot.on" : "security.secureBoot.off");
        public string UacStatusText => Loc.T(_health.IsUacEnabled ? "security.uac.on" : "security.uac.off");
        public string TpmStatusText => Loc.T(_health.IsTpmEnabled ? "security.tpm.on" : "security.tpm.off");
        public string LastUpdateCheck => _health.LastUpdateCheck;
        public bool IsDefenderActive => _health.IsDefenderActive;
        public bool IsFirewallActive => _health.IsFirewallActive;
        public bool IsSecureBootEnabled => _health.IsSecureBootEnabled;
        public bool IsUacEnabled => _health.IsUacEnabled;
        public bool IsTpmEnabled => _health.IsTpmEnabled;

        public event PropertyChangedEventHandler? PropertyChanged;

        public UpdateSecurityPage(ISystemService systemService)
        {
            InitializeComponent();
            _systemService = systemService;
            this.DataContext = this;
            
            // DAPP-41: dil değişince tüm görüntü metinleri yeniden okunur.
            Loc.LanguageChanged += (s, e) => OnPropertyChanged(string.Empty);

            this.Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                Health = await _systemService.GetSystemHealthAsync();
                
                // Notify helpers changed
                OnPropertyChanged(nameof(DefenderStatusText));
                OnPropertyChanged(nameof(FirewallStatusText));
                OnPropertyChanged(nameof(SecureBootStatusText));
                OnPropertyChanged(nameof(UacStatusText));
                OnPropertyChanged(nameof(TpmStatusText));
                OnPropertyChanged(nameof(LastUpdateCheck));
                OnPropertyChanged(nameof(IsDefenderActive));
                OnPropertyChanged(nameof(IsFirewallActive));
                OnPropertyChanged(nameof(IsSecureBootEnabled));
                OnPropertyChanged(nameof(IsUacEnabled));
                OnPropertyChanged(nameof(IsTpmEnabled));
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Security Load Error: {ex.Message}");
            }
        }

        private async void OnLaunchUpdateClick(object sender, RoutedEventArgs e)
        {
            await _systemService.LaunchWindowsUpdateAsync();
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
