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
        public string DefenderStatusText => _health.IsDefenderActive ? "Active & Protecting" : "Action Recommended";
        public string FirewallStatusText => _health.IsFirewallActive ? "Active & Monitoring" : "Protection Off";
        public string SecureBootStatusText => _health.IsSecureBootEnabled ? "Enabled (Secure)" : "Disabled / Legacy";
        public string UacStatusText => _health.IsUacEnabled ? "Enabled (Safe)" : "Disabled (Not Recommended)";
        public string TpmStatusText => _health.IsTpmEnabled ? "TPM 2.0 Ready" : "Not Detected / Disabled";
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
