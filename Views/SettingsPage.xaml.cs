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
        private readonly ILocalizationService _localization;
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
            _localization = serviceProvider.GetRequiredService<ILocalizationService>();
            _logger = serviceProvider.GetRequiredService<ILoggerService>();

            LoadSettings();
            _isInitializing = false;
        }

        private void LoadSettings()
        {
            HardwareMonitoringSwitch.IsChecked = _settingsService.Settings.EnableDeepHardwareMonitoring;

            // DAPP-41: kayıtlı tercih (System/Turkish/English) seçiciye yansıtılır.
            LanguageComboBox.SelectedIndex = _localization.Language switch
            {
                AppLanguage.Turkish => 1,
                AppLanguage.English => 2,
                _ => 0
            };
        }

        /// <summary>Dil değişimi: anında uygulanır ve ayar dosyasına yazılır (yeniden başlatma yok).</summary>
        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var mode = LanguageComboBox.SelectedIndex switch
            {
                1 => AppLanguage.Turkish,
                2 => AppLanguage.English,
                _ => AppLanguage.System
            };

            _localization.SetLanguage(mode);
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
                Loc.T("settings.clearLogsConfirmTitle"),
                Loc.T("settings.clearLogsConfirmMsg"));

            if (confirmed)
            {
                _timelineService.Clear();
                await _alertService.ShowSuccessAsync(
                    Loc.T("settings.clearLogsSuccessTitle"),
                    Loc.T("settings.clearLogsSuccessMsg"));
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
                await _alertService.ShowErrorAsync(Loc.T("settings.thirdPartyTitle"), Loc.T("settings.invalidSource"));
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
                    Loc.T("settings.thirdPartyTitle"),
                    Loc.T("settings.browserOpenFailed", GlobalConfig.Win11DebloatUrl));
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
                await _alertService.ShowErrorAsync(Loc.T("settings.feedbackTitle"), Loc.T("settings.feedbackInvalid"));
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
                    Loc.T("settings.feedbackTitle"),
                    Loc.T("settings.browserOpenFailed", GlobalConfig.IssuesUrl));
            }
        }

        private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
        {
            CheckUpdateButton.IsEnabled = false;
            CheckUpdateButton.Content = Loc.T("settings.checking");

            try
            {
                var (hasUpdate, version, url) = await _updateService.CheckForUpdatesAsync();

                if (hasUpdate)
                {
                    var confirmed = await _alertService.ShowConfirmAsync(
                        Loc.T("settings.updateAvailableTitle"),
                        Loc.T("settings.updateAvailableMsg", version));

                    if (confirmed)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = string.IsNullOrEmpty(url) ? RbnSystemPro.Helpers.GlobalConfig.ProjectUrl : url,
                            UseShellExecute = true
                        });

                        _timelineService.AddEventKey("settings.updateDownloadTimeline", "settings.updateDownloadTimelineDesc",
                            RbnSystemPro.Models.TimelineEventType.Installation, null, new[] { version ?? string.Empty });
                    }
                }
                else
                {
                    await _alertService.ShowSuccessAsync(
                        Loc.T("settings.upToDateTitle"),
                        Loc.T("settings.upToDateMsg", GlobalConfig.AppName));
                }
            }
            catch (Exception ex)
            {
                await _alertService.ShowErrorAsync(
                    Loc.T("alert.errorTitle"),
                    Loc.T("settings.updateCheckFailed", ex.Message));
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
                CheckUpdateButton.Content = Loc.T("settings.checkUpdates");
            }
        }
    }
}
