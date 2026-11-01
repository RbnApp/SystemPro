using System.Windows;
using System.Windows.Controls;
using RbnSystemPro.Services;
using RbnSystemPro.ViewModels;

namespace RbnSystemPro.Views
{
    public partial class WifiNetworksPage : Page
    {
        public NetworkViewModel ViewModel { get; }

        public WifiNetworksPage(NetworkViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            
            try
            {
                InitializeComponent();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(Loc.T("internal.initError", ex.Message, ex.InnerException?.Message), Loc.T("alert.criticalErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnConnectBtnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string password = WifiPasswordBox.Password;
                ViewModel.ConnectWithPasswordCommand.Execute(password);
                WifiPasswordBox.Clear();
            }
            catch (System.Exception ex)
            {
                 MessageBox.Show(Loc.T("internal.connectError", ex.Message), Loc.T("alert.errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}
