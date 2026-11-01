using System.Windows;
using System.Windows.Controls;
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
                MessageBox.Show($"Error initializing WifiNetworksPage: {ex.Message}\n\n{ex.InnerException?.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                 MessageBox.Show($"Error connecting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
