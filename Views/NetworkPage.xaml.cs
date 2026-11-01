using System.Windows;
using System.Windows.Controls;
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
                    MessageBox.Show("Error: NetworkViewModel is null.", "Internal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (NavigationService == null)
                {
             	      MessageBox.Show("Error: NavigationService is not available.", "Internal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                NavigationService.Navigate(new WifiNetworksPage(ViewModel));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Navigation Error: {ex.Message}\n\n{ex.InnerException?.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
