using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RbnSystemPro.ViewModels;
using RbnSystemPro.Services;

namespace RbnSystemPro.Views
{
    public partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
            WelcomeText.Text = RbnSystemPro.Helpers.GlobalConfig.AppName;
        }

        private async void OnDeployCardClick(object sender, MouseButtonEventArgs e)
        {
            var services = ((App)Application.Current).GetServiceProvider();
            var alertService = services.GetRequiredService<IAlertService>();
            var deployService = services.GetRequiredService<IDeployService>();
            var mainVm = services.GetRequiredService<MainViewModel>();

            // Kaynak adresi ve yönetici uyarısı ONAYDAN ÖNCE gösterilir (denetim #1).
            // Betiğin kendisi bu kartta değiştirilmedi; yerel kopya/SHA kararı patrona ait.
            var confirmed = await alertService.ShowConfirmAsync(
                "Windows Deploy",
                "İnternetten bir betik indirilip YÖNETİCİ yetkisiyle çalıştırılacak.\n\n" +
                "Kaynak: https://debloat.raphi.re/ (Raphire/Win11Debloat)\n\n" +
                "Betik Windows ayarlarını değiştirir; etkisi geri alınamaz. Devam edilsin mi?");

            if (confirmed)
            {
                try
                {
                    mainVm.IsBusy = true;
                    await deployService.RunRaphireWinDeployAsync();
                    await alertService.ShowSuccessAsync("Windows Deploy", "Windows Deploy script executed successfully.");
                }
                catch (System.Exception ex)
                {
                    await alertService.ShowErrorAsync("Windows Deploy Error", ex.Message);
                }
                finally
                {
                    mainVm.IsBusy = false;
                }
            }
        }

        private void OnProgramsCardClick(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage<ProgramsPage>();
        }

        private async void OnActivationCardClick(object sender, MouseButtonEventArgs e)
        {
            var mainVm = ((App)Application.Current).GetServiceProvider().GetRequiredService<MainViewModel>();
            await mainVm.ActivateWindowsAsync();
        }

        private void OnGpuDriverCardClick(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage<GpuDriversPage>();
        }

        private void OnDriversCardClick(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage<DriversPage>();
        }

        private void OnCleanerCardClick(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage<CleanerPage>();
        }

        private void OnUpdateSecurityCardClick(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage<UpdateSecurityPage>();
        }

        private void OnNetworkCardClick(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage<NetworkPage>();
        }

        private void OnPerformanceCardClick(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage<PerformancePage>();
        }

        private void OnViewTimelineClick(object sender, RoutedEventArgs e)
        {
            NavigateToPage<TimelinePage>();
        }

        private void NavigateToPage<T>() where T : Page
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var page = ((App)Application.Current).GetServiceProvider().GetRequiredService<T>();
                mainWindow.RootFrame.Navigate(page);
            }
        }
    }
}
