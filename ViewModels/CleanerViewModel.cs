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
        private string _statusMessage = "Ready to scan...";
        private string _tempFileSizeStr = "0 MB";
        private int _registryItemsFound;

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
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

            var oldStatus = browser.Status;
            browser.Status = "Cleaning...";
            StatusMessage = $"Cleaning {browser.Name}...";

            bool success = await _cleanerService.CleanBrowserAsync(browser);
            
            browser.Status = success ? "Cleaned" : "Failed";
            StatusMessage = success ? $"{browser.Name} cleaned." : $"Failed to clean {browser.Name}.";
            
            if (success)
            {
                await _alertService.ShowSuccessAsync("Browser Cleaned", $"{browser.Name} cache has been removed.");
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
            StatusMessage = "Scanning for junk files...";
            
            long size = await _cleanerService.GetTempFileSizeAsync();
            TempFileSizeStr = $"{(size / (1024.0 * 1024.0)):N2} MB";
            
            RegistryItemsFound = await _cleanerService.ScanRegistryAsync();
            
            IsBusy = false;
            StatusMessage = "Scan complete.";
        }

        private async Task CleanAsync()
        {
            // Yıkıcı işlem: neyin silineceği ONAYDAN ÖNCE söylenir (denetim #13).
            var confirmed = await _alertService.ShowConfirmAsync(
                "Sistem Temizliği",
                "Geçici dosyalar (%TEMP%, C:\\Windows\\Temp), kullanılmayan kayıt defteri kayıtları ve " +
                "seçili tarayıcı önbellekleri SİLİNECEK.\n\nDevam edilsin mi?");

            if (!confirmed)
            {
                StatusMessage = "Temizlik iptal edildi.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Cleaning selected items...";

            await _cleanerService.CleanTempFilesAsync();

            int registryCleaned = await _cleanerService.CleanRegistryAsync();

            foreach (var browser in Browsers.Where(b => b.IsSelected))
            {
                browser.Status = "Cleaning...";
                await _cleanerService.CleanBrowserAsync(browser);
                browser.Status = "Cleaned";
            }

            IsBusy = false;
            StatusMessage = "Cleanup finished successfully.";
            TempFileSizeStr = "0 MB";
            RegistryItemsFound = 0;

            await _alertService.ShowSuccessAsync("Cleanup Successful", "System junk and selected browser data have been removed.");
        }
    }
}
