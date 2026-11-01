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
        // DAPP-41: durum metni anahtar + argüman; dil değişince kendiliğinden çevrilir.
        private string _statusKey = "drivers.status.select";
        private object?[] _statusArgs = System.Array.Empty<object?>();
        private bool _isWingetInstalled;

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage => Loc.T(_statusKey, _statusArgs);

        private void SetStatus(string key, params object?[] args)
        {
            _statusKey = key;
            _statusArgs = args;
            OnPropertyChanged(nameof(StatusMessage));
        }
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
            driver.StatusKey = "status.installing";
            SetStatus("drivers.status.installing", driver.Name);

            bool success = await _driverService.InstallDriverAsync(driver);

            if (success)
            {
                driver.IsInstalled = true;
                driver.StatusKey = "status.installed";
                SetStatus("drivers.status.installed", driver.Name);
            }
            else
            {
                driver.StatusKey = "status.failed";
                SetStatus("drivers.status.failed", driver.Name);
                await _alertService.ShowErrorAsync(Loc.T("drivers.installFailedTitle"), Loc.T("drivers.installFailedMsg", driver.Name));
            }

            IsBusy = false;
        }

        private async Task EnableWingetAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            SetStatus("programs.status.enablingWinget");

            bool success = await _softwareService.EnableWingetAsync();

            IsWingetInstalled = success;
            IsBusy = false;

            if (success)
            {
                SetStatus("programs.status.wingetEnabled");
                await _alertService.ShowSuccessAsync(Loc.T("programs.wingetReadyTitle"), Loc.T("programs.wingetReadyMsg"));
            }
            else
            {
                SetStatus("programs.status.wingetEnableFailed");
                await _alertService.ShowErrorAsync(Loc.T("programs.enableFailedTitle"), Loc.T("programs.enableFailedMsg"));
            }
        }
    }
}
