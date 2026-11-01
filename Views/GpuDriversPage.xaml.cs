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
        }

        private async void GpuDriversPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDriversAsync();
        }

        private async System.Threading.Tasks.Task LoadDriversAsync()
        {
            try
            {
                ShowLoading("Loading GPU drivers...");

                // Get hardware info to detect current GPU
                var hardwareInfo = await _hardwareService.GetSystemInfoAsync();
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
                await _alertService.ShowErrorAsync("Error", "Failed to load GPU drivers. Please try again.");
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
            CurrentDriverDesc.Text = driver.Description;
            CurrentInstalledVersion.Text = driver.InstalledVersion;
            CurrentLatestVersion.Text = driver.LatestVersion;
            CurrentDriverTypeText.Text = driver.DriverType;
            CurrentManualDownloadButton.Tag = driver;

            if (driver.IsInstalled)
            {
                CurrentInstallButton.Visibility = Visibility.Collapsed;

                if (driver.UpdateAvailable)
                {
                    CurrentUpdateButton.Visibility = Visibility.Visible;
                    CurrentStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107"));
                    CurrentStatusText.Text = "Update Available";
                }
                else
                {
                    CurrentUpdateButton.Visibility = Visibility.Collapsed;
                    CurrentStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745"));
                    CurrentStatusText.Text = "Up to Date";
                }
            }
            else
            {
                CurrentInstallButton.Visibility = Visibility.Visible;
                CurrentUpdateButton.Visibility = Visibility.Collapsed;
                CurrentStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                CurrentStatusText.Text = "Not Installed";
            }
        }

        private async void InstallCurrentDriver_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGpuDriver == null) return;

            var result = await _alertService.ShowConfirmAsync(
                "Install GPU Driver",
                $"This will install {_currentGpuDriver.Name}. The installation will run silently in the background. Continue?");

            if (result)
            {
                await InstallDriverAsync(_currentGpuDriver);
            }
        }

        private async void UpdateCurrentDriver_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGpuDriver == null) return;

            var result = await _alertService.ShowConfirmAsync(
                "Update GPU Driver",
                $"This will update {_currentGpuDriver.Name} to the latest version. The update will run silently in the background. Continue?");

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
            
            await _alertService.ShowSuccessAsync("Download Driver", 
                "Your browser has been opened to the official download page. Please download and install the driver manually.");
        }

        private async System.Threading.Tasks.Task InstallDriverAsync(GpuDriverModel driver)
        {
            try
            {
                ShowLoading($"Installing {driver.Name}...");

                var success = await _gpuDriverService.InstallDriverAsync(driver);

                if (success)
                {
                    await _alertService.ShowSuccessAsync(
                        "Installation Complete",
                        $"{driver.Name} has been installed successfully. A system restart may be required.");
                    
                    await LoadDriversAsync(); // Refresh the list
                }
                else
                {
                    // Drivers without a winget package (NVIDIA / AMD / Intel) are manual downloads
                    if (driver.Manufacturer == "NVIDIA" || string.IsNullOrEmpty(driver.Id))
                    {
                        await _alertService.ShowWarningAsync(
                            "Manual Download Required",
                            $"{driver.Name} is not available through automated installation. Your browser has been opened to the official {driver.Manufacturer} download page. Please download and install the driver manually.");
                    }
                    else
                    {
                        await _alertService.ShowErrorAsync(
                            "Installation Failed",
                            $"Failed to install {driver.Name}. Please check the logs for details.");
                    }
                }

                HideLoading();
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error installing {driver.Name}", ex);
                await _alertService.ShowErrorAsync("Error", $"An error occurred while installing {driver.Name}.");
                HideLoading();
            }
        }

        private async System.Threading.Tasks.Task UpdateDriverAsync(GpuDriverModel driver)
        {
            try
            {
                ShowLoading($"Updating {driver.Name}...");

                var success = await _gpuDriverService.UpdateDriverAsync(driver);

                if (success)
                {
                    await _alertService.ShowSuccessAsync(
                        "Update Complete",
                        $"{driver.Name} has been updated successfully. A system restart may be required.");
                    
                    await LoadDriversAsync(); // Refresh the list
                }
                else
                {
                    // Drivers without a winget package (NVIDIA / AMD / Intel) are manual downloads
                    if (driver.Manufacturer == "NVIDIA" || string.IsNullOrEmpty(driver.Id))
                    {
                        await _alertService.ShowWarningAsync(
                            "Manual Download Required",
                            $"{driver.Name} updates are not available through automated installation. Your browser has been opened to the official {driver.Manufacturer} download page. Please download and install the latest driver manually.");
                    }
                    else
                    {
                        await _alertService.ShowErrorAsync(
                            "Update Failed",
                            $"Failed to update {driver.Name}. Please check the logs for details.");
                    }
                }

                HideLoading();
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error updating {driver.Name}", ex);
                await _alertService.ShowErrorAsync("Error", $"An error occurred while updating {driver.Name}.");
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
                return isInstalled ? "Update Driver" : "Install Driver";
            }
            return "Install Driver";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
