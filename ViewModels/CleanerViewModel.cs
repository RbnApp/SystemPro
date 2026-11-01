using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using RbnSystemPro.Helpers;
using RbnSystemPro.Services;
using RbnSystemPro.Models;
using System.Linq;

namespace RbnSystemPro.ViewModels
{
    public class CleanerViewModel : ObservableObject
    {
        private readonly ICleanerService _cleanerService;
        private readonly IAlertService _alertService;
        private readonly ILoggerService _logger;

        private bool _isBusy;
        // DAPP-41: durum metni anahtar + argüman; dil değişince kendiliğinden çevrilir.
        private string _statusKey = "cleaner.status.ready";
        private object?[] _statusArgs = Array.Empty<object?>();
        private string _tempFileSizeStr = "0 MB";
        private int _registryItemsFound;

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage => Loc.T(_statusKey, _statusArgs);

        private void SetStatus(string key, params object?[] args)
        {
            _statusKey = key;
            _statusArgs = args;
            OnPropertyChanged(nameof(StatusMessage));
        }
        public string TempFileSizeStr { get => _tempFileSizeStr; set => SetProperty(ref _tempFileSizeStr, value); }
        public int RegistryItemsFound { get => _registryItemsFound; set => SetProperty(ref _registryItemsFound, value); }

        public ObservableCollection<BrowserModel> Browsers { get; set; } = new();
        
        public ICommand ScanCommand { get; }
        public ICommand CleanCommand { get; }
        public ICommand CleanSpecificBrowserCommand { get; }

        public CleanerViewModel(ICleanerService cleanerService, IAlertService alertService, ILoggerService logger)
        {
            _cleanerService = cleanerService;
            _alertService = alertService;
            _logger = logger;

            ScanCommand = new DelegateCommand(async () => await ScanAsync());
            CleanCommand = new DelegateCommand(async () => await CleanAsync());
            CleanSpecificBrowserCommand = new DelegateCommand<BrowserModel>(async (b) => await CleanSpecificBrowserAsync(b));

            LoadBrowsers();
        }

        private async Task CleanSpecificBrowserAsync(BrowserModel? browser)
        {
            if (browser == null || IsBusy) return;

            browser.StatusKey = "status.cleaning";
            SetStatus("cleaner.status.cleaningBrowser", browser.Name);

            bool success = await _cleanerService.CleanBrowserAsync(browser);
            
            browser.StatusKey = success ? "status.cleaned" : "status.failed";
            if (success) SetStatus("cleaner.status.browserCleaned", browser.Name);
            else SetStatus("cleaner.status.browserFailed", browser.Name);
            
            if (success)
            {
                await _alertService.ShowSuccessAsync(Loc.T("cleaner.browserCleanedTitle"), Loc.T("cleaner.browserCleanedMsg", browser.Name));
            }
        }

        private async void LoadBrowsers()
        {
            var browsers = await _cleanerService.GetInstalledBrowsersAsync();
            Browsers.Clear();
            foreach (var b in browsers) Browsers.Add(b);
        }

        private async Task ScanAsync()
        {
            IsBusy = true;
            SetStatus("cleaner.status.scanning");
            
            long size = await _cleanerService.GetTempFileSizeAsync();
            TempFileSizeStr = $"{(size / (1024.0 * 1024.0)):N2} MB";
            
            RegistryItemsFound = await _cleanerService.ScanRegistryAsync();
            
            IsBusy = false;
            SetStatus("cleaner.status.scanDone");
        }

        private async Task CleanAsync()
        {
            // Yıkıcı işlem: neyin silineceği ONAYDAN ÖNCE söylenir (denetim #13).
            var confirmed = await _alertService.ShowConfirmAsync(
                Loc.T("cleaner.confirmTitle"),
                Loc.T("cleaner.confirmMsg"));

            if (!confirmed)
            {
                SetStatus("cleaner.status.cancelled");
                return;
            }

            IsBusy = true;
            SetStatus("cleaner.status.cleaning");

            await _cleanerService.CleanTempFilesAsync();

            int registryCleaned = await _cleanerService.CleanRegistryAsync();

            foreach (var browser in Browsers.Where(b => b.IsSelected))
            {
                browser.StatusKey = "status.cleaning";
                await _cleanerService.CleanBrowserAsync(browser);
                browser.StatusKey = "status.cleaned";
            }

            IsBusy = false;
            SetStatus("cleaner.status.done");
            TempFileSizeStr = "0 MB";
            RegistryItemsFound = 0;

            await _alertService.ShowSuccessAsync(Loc.T("cleaner.successTitle"), Loc.T("cleaner.successMsg"));
        }
    }
}
