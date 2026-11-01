using System.Threading.Tasks;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace RbnSystemPro.Services
{
    public interface IAlertService
    {
        Task ShowAsync(string title, string message, ControlAppearance appearance = ControlAppearance.Primary);
        Task ShowErrorAsync(string title, string message, System.Exception? ex = null);
        Task ShowWarningAsync(string title, string message);
        Task ShowSuccessAsync(string title, string message);
        Task<bool> ShowConfirmAsync(string title, string message);
    }

    public class AlertService : IAlertService
    {
        private readonly IContentDialogService _contentDialogService;
        private readonly ILoggerService _loggerService;

        public AlertService(IContentDialogService contentDialogService, ILoggerService loggerService)
        {
            _contentDialogService = contentDialogService;
            _loggerService = loggerService;
        }

        public async Task ShowAsync(string title, string message, ControlAppearance appearance = ControlAppearance.Primary)
        {
            await _loggerService.LogInfoAsync($"Alert Shown: {title} - {message}");
            await _contentDialogService.ShowAlertAsync(title, message, "OK");
        }

        public async Task ShowErrorAsync(string title, string message, System.Exception? ex = null)
        {
            await _loggerService.LogErrorAsync($"Error Alert: {title} - {message}", ex);
            await _contentDialogService.ShowAlertAsync(title, message, "OK");
        }

        public async Task ShowWarningAsync(string title, string message)
        {
            await _loggerService.LogWarningAsync($"Warning Alert: {title} - {message}");
            await _contentDialogService.ShowAlertAsync(title, message, "OK");
        }

        public async Task ShowSuccessAsync(string title, string message)
        {
            await _loggerService.LogInfoAsync($"Success Alert: {title} - {message}");
            await _contentDialogService.ShowAlertAsync(title, message, "OK");
        }

        public async Task<bool> ShowConfirmAsync(string title, string message)
        {
            await _loggerService.LogInfoAsync($"Confirmation Dialog: {title} - {message}");
            
            // Check if we're on the UI thread
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                // Already on UI thread, execute directly
                var dialogResult = await _contentDialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions()
                    {
                        Title = title,
                        Content = message,
                        PrimaryButtonText = "Yes",
                        CloseButtonText = "No"
                    });

                return dialogResult == ContentDialogResult.Primary;
            }
            else
            {
                // Not on UI thread, use InvokeAsync to properly handle async operation
                return await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var dialogResult = await _contentDialogService.ShowSimpleDialogAsync(
                        new SimpleContentDialogCreateOptions()
                        {
                            Title = title,
                            Content = message,
                            PrimaryButtonText = "Yes",
                            CloseButtonText = "No"
                        });

                    return dialogResult == ContentDialogResult.Primary;
                }).Task.Unwrap();
            }
        }
    }
}
