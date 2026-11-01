using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RbnSystemPro.ViewModels;
using RbnSystemPro.Services;
using Wpf.Ui.Controls;

namespace RbnSystemPro.Views
{
    public partial class MainWindow : FluentWindow
    {
        public MainViewModel ViewModel { get; }
        public string AppTitle => RbnSystemPro.Helpers.GlobalConfig.FullTitle;

        public MainWindow(MainViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();

            // Initial navigation after window is fully loaded and visual tree is ready
            Loaded += (s, e) => 
            {
                // Delay slightly to ensure Frame is fully initialized
                Dispatcher.BeginInvoke(new Action(() => Navigate(typeof(DashboardPage))));
            };
        }

        private void OnHomeButtonClick(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(DashboardPage));
        }

        private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsPage));
        }

        private void OnActivityButtonClick(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(TimelinePage));
        }

        public void Navigate(Type pageType)
        {
            try
            {
                var serviceProvider = ((App)Application.Current).GetServiceProvider();
                var page = serviceProvider.GetService(pageType);
                
                if (page != null)
                {
                    RootFrame.Navigate(page);
                }
                else
                {
                    System.Windows.MessageBox.Show($"Could not resolve page type: {pageType.Name}", "Navigation Error");
                }
            }
            catch (Exception ex)
            {
                 System.Windows.MessageBox.Show($"Navigation failed for {pageType.Name}: {ex.Message}\n\n{ex.StackTrace}", "Critical Navigation Error");
            }
        }
    }
}
