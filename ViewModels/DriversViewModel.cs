using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using RbnSystemPro.Helpers;
using RbnSystemPro.Services;
using RbnSystemPro.Models;

namespace RbnSystemPro.ViewModels
{
    public class DriversViewModel : ObservableObject
    {
        private readonly IEssentialDriverService _driverService;
        private readonly IAlertService _alertService;
        private readonly ILoggerService _logger;
        private readonly ISoftwareService _softwareService;

        private bool _isBusy;
        private string _statusMessage = "Select essential drivers to install";
        private bool _isWingetInstalled;

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsWingetInstalled { get => _isWingetInstalled; set => SetProperty(ref _isWingetInstalled, value); }

        public ObservableCollection<EssentialDriverModel> Drivers { get; set; } = new();
        
        public ICommand InstallCommand { get; }
        public ICommand EnableWingetCommand { get; }

        public DriversViewModel(IEssentialDriverService driverService, IAlertService alertService, ILoggerService logger, ISoftwareService softwareService)
        {
            _driverService = driverService;
            _alertService = alertService;
            _logger = logger;
            _softwareService = softwareService;

            InstallCommand = new DelegateCommand<EssentialDriverModel>(async (d) => await InstallDriverAsync(d));
            EnableWingetCommand = new DelegateCommand(async () => await EnableWingetAsync());

            _ = LoadDriversAsync();
        }

        private async Task LoadDriversAsync()
        {
            var drivers = _driverService.GetEssentialDrivers();
            await _driverService.CheckInstalledDriversAsync(drivers);
            
            // Check Winget Status
            IsWingetInstalled = await _softwareService.IsWingetAvailableAsync();

            Drivers.Clear();
            foreach (var d in drivers) Drivers.Add(d);
        }

        private async Task InstallDriverAsync(EssentialDriverModel driver)
        {
            if (driver == null || IsBusy) return;

            IsBusy = true;
            driver.Status = "Installing...";
            StatusMessage = $"Installing {driver.Name}...";

            bool success = await _driverService.InstallDriverAsync(driver);

            if (success)
            {
                driver.IsInstalled = true;
                driver.Status = "Installed";
                StatusMessage = $"{driver.Name} installed successfully.";
            }
            else
            {
                driver.Status = "Failed";
                StatusMessage = $"Failed to install {driver.Name}.";
                await _alertService.ShowErrorAsync("Installation Failed", $"Could not install {driver.Name}. Check logs for details.");
            }

            IsBusy = false;
        }

        private async Task EnableWingetAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            StatusMessage = "Enabling Winget...";

            bool success = await _softwareService.EnableWingetAsync();

            IsWingetInstalled = success;
            IsBusy = false;

            if (success)
            {
                StatusMessage = "Winget enabled successfully.";
                await _alertService.ShowSuccessAsync("Winget Ready", "Winget has been enabled and is ready to use.");
            }
            else
            {
                StatusMessage = "Failed to enable Winget.";
                await _alertService.ShowErrorAsync("Enable Failed", "Could not enable Winget automatically. Please install 'App Installer' from Microsoft Store.");
            }
        }
    }
}
