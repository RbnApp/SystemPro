using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using RbnSystemPro.Models;
using RbnSystemPro.Services;

namespace RbnSystemPro.Views
{
    public partial class GpuDriversPage : Page
    {
        private readonly IGpuDriverService _gpuDriverService;
        private RbnSystemPro.Models.HardwareInfo? _hardwareInfo;
        private readonly IHardwareService _hardwareService;
        private readonly ILoggerService _logger;
        private readonly IAlertService _alertService;
        private GpuDriverModel? _currentGpuDriver;

        public GpuDriversPage(
            IGpuDriverService gpuDriverService,
            IHardwareService hardwareService,
            ILoggerService logger,
            IAlertService alertService)
        {
            InitializeComponent();
            _gpuDriverService = gpuDriverService;
            _hardwareService = hardwareService;
            _logger = logger;
            _alertService = alertService;

            Loaded += GpuDriversPage_Loaded;

            // DAPP-41: dil değişince donanım metinleri (ör. GPU belleği) yeniden çevrilir.
            Loc.LanguageChanged += (_, _) =>
            {
                if (_hardwareInfo != null) GpuMemoryText.Text = _hardwareInfo.GpuMemory;
            };
        }

        private async void GpuDriversPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDriversAsync();
        }

        private async System.Threading.Tasks.Task LoadDriversAsync()
        {
            try
            {
                ShowLoading(Loc.T("gpu.loadingGpuDrivers"));

                // Get hardware info to detect current GPU
                var hardwareInfo = await _hardwareService.GetSystemInfoAsync();
                _hardwareInfo = hardwareInfo;
                var gpuManufacturer = _gpuDriverService.DetectCurrentGpuManufacturer(hardwareInfo.GpuName);

                // CurrentGpuText.Text = $"Detected GPU: {hardwareInfo.GpuName} ({gpuManufacturer})";
                CurrentGpuText.Text = hardwareInfo.GpuName;
                GpuMemoryText.Text = hardwareInfo.GpuMemory;

                // Get all available drivers (pass GPU name for version checking)
                var drivers = await _gpuDriverService.GetAvailableDriversAsync(hardwareInfo.GpuName);

                // Mark current GPU driver
                foreach (var driver in drivers)
                {
                    driver.IsCurrentGpu = driver.Manufacturer == gpuManufacturer;
                }

                // Set current GPU driver (MUST prefer established installation first)
                _currentGpuDriver = drivers.FirstOrDefault(d => d.IsCurrentGpu && d.IsInstalled) 
                                   ?? drivers.FirstOrDefault(d => d.IsCurrentGpu && d.Manufacturer == gpuManufacturer);

                if (_currentGpuDriver != null)
                {
                    UpdateCurrentDriverUI(_currentGpuDriver);
                    CurrentDriverCard.Visibility = Visibility.Visible;
                    NoGpuCard.Visibility = Visibility.Collapsed;
                    
                    // Set gradient color based on manufacturer
                    SetDriverIconGradient(_currentGpuDriver.Manufacturer);
                }
                else
                {
                    CurrentDriverCard.Visibility = Visibility.Collapsed;
                    NoGpuCard.Visibility = Visibility.Visible;
                }

                // Populate other drivers list - EXCLUDE only the primary displayed GPU driver so alternate branches (e.g. Studio vs Game Ready) remain accessible
                var otherDrivers = drivers.Where(d => d != _currentGpuDriver).ToList();
                AllDriversList.ItemsSource = otherDrivers;

                HideLoading();
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Error loading GPU drivers", ex);
                await _alertService.ShowErrorAsync(Loc.T("alert.errorTitle"), Loc.T("gpu.loadFailed"));
                HideLoading();
            }
        }

        private void SetDriverIconGradient(string manufacturer)
        {
            LinearGradientBrush gradient;
            
            switch (manufacturer.ToUpper())
            {
                case "NVIDIA":
                    gradient = (LinearGradientBrush)FindResource("NvidiaGradient");
                    break;
                case "AMD":
                    gradient = (LinearGradientBrush)FindResource("AmdGradient");
                    break;
                case "INTEL":
                    gradient = (LinearGradientBrush)FindResource("IntelGradient");
                    break;
                default:
                    gradient = (LinearGradientBrush)FindResource("NvidiaGradient");
                    break;
            }
            
            CurrentDriverIcon.Background = gradient;
            
            // Extract colors for dynamic UI accents
            var mainColor = gradient.GradientStops[0].Color;
            var alphaColor = Color.FromArgb(26, mainColor.R, mainColor.G, mainColor.B); // ~10% opacity

            // Update Dynamic Resources for Tabs and GPU Text
            Resources["GpuAccentBrush"] = new SolidColorBrush(mainColor);
            Resources["GpuAccentAlphaBrush"] = new SolidColorBrush(alphaColor);

            // Update Header Icon & Shadow
            HeaderIcon.Foreground = new SolidColorBrush(mainColor);
            HeaderIconShadow.Color = mainColor;
            
            // Update shadow color to match gradient for the card icon
            if (CurrentDriverIcon.Effect is DropShadowEffect shadow)
            {
                shadow.Color = mainColor;
            }
        }

        private void UpdateCurrentDriverUI(GpuDriverModel driver)
        {
            CurrentDriverName.Text = driver.Name;
            CurrentDriverDesc.Text = driver.DescriptionText;
            CurrentInstalledVersion.Text = driver.InstalledVersionText;
            CurrentLatestVersion.Text = driver.LatestVersionText;
            CurrentDriverTypeText.Text = driver.DriverType;
            CurrentManualDownloadButton.Tag = driver;

            if (driver.IsInstalled)
            {
                CurrentInstallButton.Visibility = Visibility.Collapsed;

                if (driver.UpdateAvailable)
                {
                    CurrentUpdateButton.Visibility = Visibility.Visible;
                    CurrentStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107"));
                    CurrentStatusText.Text = Loc.T("gpu.updateAvailable");
                }
                else
                {
                    CurrentUpdateButton.Visibility = Visibility.Collapsed;
                    CurrentStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                    CurrentStatusText.Text = Loc.T("gpu.upToDate");
                }
            }
            else
            {
                CurrentInstallButton.Visibility = Visibility.Visible;
                CurrentUpdateButton.Visibility = Visibility.Collapsed;
                CurrentStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                CurrentStatusText.Text = Loc.T("gpu.notInstalled");
            }
        }

        private async void InstallCurrentDriver_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGpuDriver == null) return;

            var result = await _alertService.ShowConfirmAsync(
                Loc.T("gpu.installTitle"),
                Loc.T("gpu.installConfirm", _currentGpuDriver.Name));

            if (result)
            {
                await InstallDriverAsync(_currentGpuDriver);
            }
        }

        private async void UpdateCurrentDriver_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGpuDriver == null) return;

            var result = await _alertService.ShowConfirmAsync(
                Loc.T("gpu.updateTitle"),
                Loc.T("gpu.updateConfirm", _currentGpuDriver.Name));

            if (result)
            {
                await UpdateDriverAsync(_currentGpuDriver);
            }
        }

        private async void DownloadDriver_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not GpuDriverModel driver)
                return;

            string url = !string.IsNullOrWhiteSpace(driver.DownloadUrl)
                ? driver.DownloadUrl
                : driver.Manufacturer.ToUpper() switch
                {
                    "NVIDIA" => "https://www.nvidia.com/download/index.aspx",
                    "AMD" => "https://www.amd.com/en/support",
                    "INTEL" => "https://www.intel.com/content/www/us/en/download-center/home.html",
                    _ => "https://www.google.com/search?q=" + Uri.EscapeDataString(driver.Name + " download")
                };

            await _logger.LogInfoAsync($"Opening browser for manual download: {driver.Name} ({url})");
            
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
            
            await _alertService.ShowSuccessAsync(Loc.T("gpu.downloadOpenedTitle"),
                Loc.T("gpu.downloadOpenedMsg"));
        }

        private async System.Threading.Tasks.Task InstallDriverAsync(GpuDriverModel driver)
        {
            try
            {
                ShowLoading(Loc.T("gpu.status.installing", driver.Name));

                var success = await _gpuDriverService.InstallDriverAsync(driver);

                if (success)
                {
                    await _alertService.ShowSuccessAsync(
                        Loc.T("gpu.installCompleteTitle"),
                        Loc.T("gpu.installCompleteMsg", driver.Name));
                    
                    await LoadDriversAsync(); // Refresh the list
                }
                else
                {
                    // Drivers without a winget package (NVIDIA / AMD / Intel) are manual downloads
                    if (driver.Manufacturer == "NVIDIA" || string.IsNullOrEmpty(driver.Id))
                    {
                        await _alertService.ShowWarningAsync(
                            Loc.T("gpu.manualRequiredTitle"),
                            Loc.T("gpu.manualInstallMsg", driver.Name, driver.Manufacturer));
                    }
                    else
                    {
                        await _alertService.ShowErrorAsync(
                            Loc.T("gpu.installFailedTitle"),
                            Loc.T("gpu.installFailedMsg", driver.Name));
                    }
                }

                HideLoading();
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error installing {driver.Name}", ex);
                await _alertService.ShowErrorAsync(Loc.T("alert.errorTitle"), Loc.T("gpu.installErrorMsg", driver.Name));
                HideLoading();
            }
        }

        private async System.Threading.Tasks.Task UpdateDriverAsync(GpuDriverModel driver)
        {
            try
            {
                ShowLoading(Loc.T("gpu.status.updating", driver.Name));

                var success = await _gpuDriverService.UpdateDriverAsync(driver);

                if (success)
                {
                    await _alertService.ShowSuccessAsync(
                        Loc.T("gpu.updateCompleteTitle"),
                        Loc.T("gpu.updateCompleteMsg", driver.Name));
                    
                    await LoadDriversAsync(); // Refresh the list
                }
                else
                {
                    // Drivers without a winget package (NVIDIA / AMD / Intel) are manual downloads
                    if (driver.Manufacturer == "NVIDIA" || string.IsNullOrEmpty(driver.Id))
                    {
                        await _alertService.ShowWarningAsync(
                            Loc.T("gpu.manualRequiredTitle"),
                            Loc.T("gpu.manualUpdateMsg", driver.Name, driver.Manufacturer));
                    }
                    else
                    {
                        await _alertService.ShowErrorAsync(
                            Loc.T("gpu.updateFailedTitle"),
                            Loc.T("gpu.updateFailedMsg", driver.Name));
                    }
                }

                HideLoading();
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error updating {driver.Name}", ex);
                await _alertService.ShowErrorAsync(Loc.T("alert.errorTitle"), Loc.T("gpu.updateErrorMsg", driver.Name));
                HideLoading();
            }
        }

        private async void RefreshDrivers_Click(object sender, RoutedEventArgs e)
        {
            await LoadDriversAsync();
        }

        private void ShowLoading(string message)
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    // Converter for Install/Update button text
    public class BoolToInstallUpdateConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isInstalled)
            {
                return isInstalled ? Loc.T("gpu.update") : Loc.T("gpu.install");
            }
            return Loc.T("gpu.install");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
