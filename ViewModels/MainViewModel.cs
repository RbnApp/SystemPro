using RbnSystemPro.Helpers;
using RbnSystemPro.Models;
using RbnSystemPro.Services;
using System.Threading.Tasks;

namespace RbnSystemPro.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IHardwareService _hardwareService;
        private readonly IAlertService _alertService;
        private readonly ILoggerService _logger;
        private readonly ITimelineService _timeline;
        private HardwareInfo _hardwareInfo = new();
        private int _cores;
        private int _logicalProcessors;
        private bool _hasCpuInfo;
        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private readonly IBiosService _biosService;

        public System.Windows.Input.ICommand ActivateCommand { get; }
        public System.Windows.Input.ICommand CheckBiosUpdateCommand { get; }
        public System.Windows.Input.ICommand DownloadBiosCommand { get; }

        public HardwareInfo HardwareInfo
        {
            get => _hardwareInfo;
            set => SetProperty(ref _hardwareInfo, value);
        }

        /// <summary>DAPP-41: değer anahtarla çevrilir; dil değişince bağ kendiliğinden tazelenir.</summary>
        public string CpuCoreInfo => _hasCpuInfo
            ? Loc.T("main.coreThread", _cores, _logicalProcessors)
            : Loc.T("common.detecting");

        public MainViewModel(IHardwareService hardwareService, IAlertService alertService, ILoggerService logger, IBiosService biosService, ITimelineService timeline)
        {
            _hardwareService = hardwareService;
            _alertService = alertService;
            _logger = logger;
            _biosService = biosService;
            _timeline = timeline;
            
            ActivateCommand = new DelegateCommand(async () => await ActivateWindowsAsync());
            CheckBiosUpdateCommand = new DelegateCommand(CheckBiosUpdate);
            DownloadBiosCommand = new DelegateCommand(DownloadBiosUpdate);

            // Denetim H5: tam WMI donanım taraması açılışın kritik yolundan çıkarıldı.
            // Pencere çizildikten sonra, uygulama boştayken başlar; ilk ekranda görünen
            // değerler aynı kalır (tarama bitince HardwareInfo güncellenir).
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                new System.Action(async () => await LoadSystemInfoAsync()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private async Task LoadSystemInfoAsync()
        {
            try
            {
                HardwareInfo = await _hardwareService.GetSystemInfoAsync();
                _cores = HardwareInfo.NumberOfCores;
                _logicalProcessors = HardwareInfo.NumberOfLogicalProcessors;
                _hasCpuInfo = true;
                OnPropertyChanged(nameof(CpuCoreInfo));
            }
            catch (System.Exception ex)
            {
                await _alertService.ShowErrorAsync(Loc.T("alert.hardwareErrorTitle"), Loc.T("alert.hardwareErrorMsg"), ex);
            }
        }

        private void CheckBiosUpdate()
        {
            if (HardwareInfo != null)
            {
                // We use a Google Search workaround since scraping is not feasible universally
                HardwareInfo.BiosUpdateUrl = _biosService.GetBiosSearchUrl(HardwareInfo.Motherboard, "");
                HardwareInfo.BiosStatusKey = "hw.onlineCheck"; 
                HardwareInfo.IsBiosUpdateAvailable = true;
                
                OnPropertyChanged(nameof(HardwareInfo));
                
                // Open browser immediately
                DownloadBiosUpdate();
            }
        }

        private void DownloadBiosUpdate()
        {
            var url = HardwareInfo?.BiosUpdateUrl;
            if (string.IsNullOrEmpty(url) && HardwareInfo != null)
            {
                 url = _biosService.GetBiosSearchUrl(HardwareInfo.Motherboard, "");
            }

            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    _ = _logger.LogWarningAsync($"BIOS indirme sayfası açılamadı: {ex.Message}");
                }
            }
        }

        public Task ActivateWindowsAsync()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:activation") { UseShellExecute = true });
            }
            catch
            {
                // Fallback
                System.Diagnostics.Process.Start("explorer.exe", "ms-settings:activation");
            }

            _timeline.AddEventKey("event.activation.title", "event.activation.desc", TimelineEventType.Info);
            return Task.CompletedTask;
        }
    }
}
