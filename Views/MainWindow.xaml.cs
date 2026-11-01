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

            // DAPP-41: dil değişince pencere ve açık sayfa bağları tazelenir.
            Loc.LanguageChanged += OnLanguageChanged;

            // DAPP-41 doğrulama: yalnız --layout-audit ile açıldığında sayfa ölçümü yapar.
            LayoutAudit.Attach(RootFrame);

            // Initial navigation after window is fully loaded and visual tree is ready
            Loaded += (s, e) => 
            {
                // Delay slightly to ensure Frame is fully initialized
                Dispatcher.BeginInvoke(new Action(() => Navigate(typeof(DashboardPage))));
            };
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // DataContext'i yeniden vermek WPF'e "tüm bağları yeniden oku" der.
            var dc = DataContext;
            DataContext = null;
            DataContext = dc;

            if (RootFrame.Content is FrameworkElement page && page.DataContext != null)
            {
                var pageDc = page.DataContext;
                page.DataContext = null;
                page.DataContext = pageDc;
            }
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
                    System.Windows.MessageBox.Show(Loc.T("internal.pageResolveError", pageType.Name), Loc.T("internal.navigationErrorTitle"));
                }
            }
            catch (Exception ex)
            {
                 System.Windows.MessageBox.Show(Loc.T("internal.navigationFailed", pageType.Name, ex.Message), Loc.T("alert.criticalErrorTitle"));
            }
        }
    }
}
