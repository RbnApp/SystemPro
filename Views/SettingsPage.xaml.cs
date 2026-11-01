using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RbnSystemPro.Services;
using Wpf.Ui.Controls;
using RbnSystemPro.Helpers;

namespace RbnSystemPro.Views
{
    public partial class SettingsPage : Page
    {
        private readonly IUpdateService _updateService;
        private readonly ITimelineService _timelineService;
        private readonly ISettingsService _settingsService;
        private readonly IAlertService _alertService;
        private readonly ILoggerService _logger;
        private bool _isInitializing = true;

        public SettingsPage()
        {
            InitializeComponent();
            
            var serviceProvider = ((App)Application.Current).GetServiceProvider();
            _updateService = serviceProvider.GetRequiredService<IUpdateService>();
            _timelineService = serviceProvider.GetRequiredService<ITimelineService>();
            _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            _alertService = serviceProvider.GetRequiredService<IAlertService>();
            _logger = serviceProvider.GetRequiredService<ILoggerService>();

            LoadSettings();
            _isInitializing = false;
        }

        private void LoadSettings()
        {
            HardwareMonitoringSwitch.IsChecked = _settingsService.Settings.EnableDeepHardwareMonitoring;
        }

        private void OnSettingChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            _settingsService.Settings.EnableDeepHardwareMonitoring = HardwareMonitoringSwitch.IsChecked ?? true;
            _settingsService.Save();
        }

        private async void OnClearLogsClick(object sender, RoutedEventArgs e)
        {
            var confirmed = await _alertService.ShowConfirmAsync(
                "Clear Logs",
                "Are you sure you want to permanently delete all activity history?");

            if (confirmed)
            {
                _timelineService.Clear();
                await _alertService.ShowSuccessAsync(
                    "Success",
                    "Activity logs have been successfully cleared.");
            }
        }

        private void OnGitHubLinkClick(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = RbnSystemPro.Helpers.GlobalConfig.ProjectUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"GitHub bağlantısı açılamadı: {ex.Message}");
            }
        }

        // Üçüncü taraf atıf: Hakkında'daki "Raphire/Win11Debloat" bağlantısı varsayılan tarayıcıda açılır.
        // Adres GlobalConfig'teki sabitten gelir; yalnız https://github.com/Raphire/ ile başlıyorsa açılır.
        // Windows Deploy davranışına dokunmaz (betiği çalıştıran yol DeployService'tedir).
        private async void OnWin11DebloatLinkClick(object sender, RoutedEventArgs e)
        {
            var url = GlobalConfig.Win11DebloatUrl;
            if (!url.StartsWith("https://github.com/Raphire/", StringComparison.OrdinalIgnoreCase))
            {
                await _alertService.ShowErrorAsync("Third-party script", "The source address is invalid.");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Win11Debloat bağlantısı açılamadı: {ex.Message}");
                await _alertService.ShowErrorAsync(
                    "Third-party script",
                    "Could not open the browser. You can type the address manually: " + GlobalConfig.Win11DebloatUrl);
            }
        }

        // Geri bildirim: varsayılan tarayıcıda GitHub'ın "yeni issue" sayfası açılır (uygulama içi pencere DEĞİL).
        // Adres GlobalConfig'teki sabitten gelir; yalnız https://github.com/RbnApp/ ile başlıyorsa açılır.
        // Önceden doldurulan tek şey sürüm + Windows sürümü; kimlik, makine adı, log gönderilmez.
        private async void OnFeedbackClick(object sender, RoutedEventArgs e)
        {
            var url = GlobalConfig.FeedbackUrl;
            if (!url.StartsWith("https://github.com/RbnApp/", StringComparison.OrdinalIgnoreCase))
            {
                await _alertService.ShowErrorAsync("Geri bildirim", "Geri bildirim adresi geçersiz.");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _ = _logger.LogWarningAsync($"Geri bildirim bağlantısı açılamadı: {ex.Message}");
                await _alertService.ShowErrorAsync(
                    "Geri bildirim",
                    "Tarayıcı açılamadı. Adresi tarayıcıya elle yazabilirsiniz: " + GlobalConfig.IssuesUrl);
            }
        }

        private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
        {
            CheckUpdateButton.IsEnabled = false;
            CheckUpdateButton.Content = "Checking...";

            try
            {
                var (hasUpdate, version, url) = await _updateService.CheckForUpdatesAsync();

                if (hasUpdate)
                {
                    var confirmed = await _alertService.ShowConfirmAsync(
                        "Update Available",
                        $"A new version (v{version}) is available. Would you like to download it now?");

                    if (confirmed)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = string.IsNullOrEmpty(url) ? RbnSystemPro.Helpers.GlobalConfig.ProjectUrl : url,
                            UseShellExecute = true
                        });
                        
                        _timelineService.AddEvent("Update Download", $"Started download for v{version}.");
                    }
                }
                else
                {
                    await _alertService.ShowSuccessAsync(
                        "Up to Date",
                        $"You are currently running the latest version of {GlobalConfig.AppName}.");
                }
            }
            catch (Exception ex)
            {
                await _alertService.ShowErrorAsync(
                    "Error",
                    "Failed to check for updates: " + ex.Message);
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
                CheckUpdateButton.Content = "Check for Updates";
            }
        }
    }
}
