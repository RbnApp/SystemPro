using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RbnSystemPro.Services;
using RbnSystemPro.Views;
using RbnSystemPro.ViewModels;
using Wpf.Ui;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RbnSystemPro
{
    public partial class App : Application
    {
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<ProgramsViewModel>();
                services.AddSingleton<CleanerViewModel>();
                services.AddSingleton<DriversViewModel>();
                services.AddSingleton<NetworkViewModel>();

                // Views
                services.AddSingleton<MainWindow>(s => new MainWindow(s.GetRequiredService<MainViewModel>()));
                services.AddSingleton<DashboardPage>();
                services.AddSingleton<ProgramsPage>();
                services.AddSingleton<GpuDriversPage>();
                services.AddSingleton<DriversPage>();
                services.AddSingleton<CleanerPage>();
                services.AddSingleton<TimelinePage>();
                services.AddSingleton<UpdateSecurityPage>();
                services.AddSingleton<PerformancePage>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<NetworkPage>();

                // Services
                services.AddSingleton<ILoggerService, LoggerService>();
                services.AddSingleton<IDeployService, DeployService>();
                services.AddSingleton<IHardwareService, HardwareService>();
                services.AddSingleton<IContentDialogService, ContentDialogService>();
                services.AddSingleton<IAlertService, AlertService>();
                services.AddSingleton<IBiosService, BiosService>();
                services.AddSingleton<ISoftwareService, SoftwareService>();
                services.AddSingleton<IGpuDriverService, GpuDriverService>();
                services.AddSingleton<ITimelineService, TimelineService>();
                services.AddSingleton<ISystemService, SystemService>();
                services.AddSingleton<IPerformanceService, PerformanceService>();
                services.AddSingleton<IUpdateService, UpdateService>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<ICleanerService, CleanerService>();
                services.AddSingleton<IEssentialDriverService, EssentialDriverService>();
                services.AddSingleton<INetworkService, NetworkService>();
            })
            .Build();

        private static Mutex? _mutex;
        private static readonly string MutexName = $"Global\\{RbnSystemPro.Helpers.GlobalConfig.AppName}_Mutex_Unique_App_ID";

        public IServiceProvider GetServiceProvider() => _host.Services;

        protected override async void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                var uiBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = RbnSystemPro.Helpers.GlobalConfig.AppName,
                    Content = "Application is already running.",
                    CloseButtonText = "Close",
                    MaxWidth = 400
                };
                await uiBox.ShowDialogAsync();
                Shutdown();
                return;
            }

            await _host.StartAsync();

            var logger = _host.Services.GetRequiredService<ILoggerService>();
            await logger.LogInfoAsync($"{RbnSystemPro.Helpers.GlobalConfig.AppName} Started");

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            
            // Set ContentDialogService host
            var contentDialogService = _host.Services.GetRequiredService<IContentDialogService>();
            contentDialogService.SetDialogHost(mainWindow.RootContentDialogPresenter);

            mainWindow.Show();

            // Set Global Exception Handlers for logging crashes
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => LogUnhandledException(ev.ExceptionObject as Exception);
            DispatcherUnhandledException += (s, ev) => LogUnhandledException(ev.Exception);
            TaskScheduler.UnobservedTaskException += (s, ev) => LogUnhandledException(ev.Exception);

            base.OnStartup(e);
        }

        private void LogUnhandledException(Exception? ex)
        {
            if (ex == null) return;
            
            try
            {
                var logger = _host.Services.GetRequiredService<ILoggerService>();
                // We use Task.Run for async logger in sync context, but in a crash, 
                // we might want a sync version. For now, let's try this.
                Task.Run(async () => await logger.LogErrorAsync("CRITICAL UNHANDLED CRASH", ex)).Wait(2000);
                
                MessageBox.Show($"A critical error occurred. Please check log.txt for details.\n\nError: {ex.Message}", 
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Last ditch effort if logger fails
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt"), 
                    $"[{DateTime.Now}] [FATAL] {ex.Message}\n{ex.StackTrace}\n");
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();

            base.OnExit(e);
        }
    }
}
