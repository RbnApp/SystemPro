using System.Windows;
using System.Windows.Controls;
using RbnSystemPro.Services;
using RbnSystemPro.ViewModels;

namespace RbnSystemPro.Views
{
    public partial class NetworkPage : Page
    {
        public NetworkViewModel ViewModel { get; }

        public NetworkPage(NetworkViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        private void OnViewWifiNetworksClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel == null)
                {
                    MessageBox.Show(Loc.T("internal.nullViewModel"), Loc.T("internal.errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (NavigationService == null)
                {
                    MessageBox.Show(Loc.T("internal.navServiceMissing"), Loc.T("internal.errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                NavigationService.Navigate(new WifiNetworksPage(ViewModel));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(Loc.T("internal.navigationError", ex.Message, ex.InnerException?.Message),
                    Loc.T("alert.errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
