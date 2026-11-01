using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using RbnSystemPro.Models;
using RbnSystemPro.Services;
using RbnSystemPro.Helpers;

namespace RbnSystemPro.ViewModels
{
    public class ProgramsViewModel : ObservableObject
    {
        private readonly ISoftwareService _softwareService;
        private readonly IAlertService _alertService;
        private readonly ILoggerService _logger;

        private bool _isBusy;
        private string _statusMessage = "Select programs to install";
        private int _progressValue;
        private bool _isWingetInstalled;

        public bool IsWingetInstalled
        {
            get => _isWingetInstalled;
            set => SetProperty(ref _isWingetInstalled, value);
        }

        public ObservableCollection<ProgramModel> Programs { get; set; } = new();
        public ObservableCollection<string> Categories { get; set; } = new();
        
        private List<ProgramModel> _allPrograms = new();
        private string _selectedCategory = "All";

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    FilterPrograms();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public ICommand InstallCommand { get; }
        public ICommand ToggleSelectCommand { get; }
        public ICommand UninstallCommand { get; }
        public ICommand EnableWingetCommand { get; }

        private bool _isUninstalling = false;

        public ProgramsViewModel(ISoftwareService softwareService, IAlertService alertService, ILoggerService logger)
        {
            _softwareService = softwareService;
            _alertService = alertService;
            _logger = logger;

            InstallCommand = new DelegateCommand(async () => await InstallSelectedAsync(), CanInstall);
            ToggleSelectCommand = new DelegateCommand(ToggleSelection);
            UninstallCommand = new DelegateCommand<ProgramModel>(UninstallProgram, CanUninstall);
            EnableWingetCommand = new DelegateCommand(async () => await EnableWingetAsync());

            LoadPrograms();
        }

        private bool CanUninstall(ProgramModel? program) => !_isUninstalling && program != null && program.IsInstalled;

        private async void UninstallProgram(ProgramModel program)
        {
            if (_isUninstalling || program == null) return;
            _isUninstalling = true;
            
            try
            {
                await UninstallProgramAsync(program);
            }
            finally
            {
                _isUninstalling = false;
                ((DelegateCommand<ProgramModel>)UninstallCommand).RaiseCanExecuteChanged();
            }
        }

        private async void LoadPrograms()
        {
            _allPrograms = _softwareService.GetAvailablePrograms();
            
            // Initial check off-thread to not block UI
            await _softwareService.CheckInstalledProgramsAsync(_allPrograms);

            // Populate Categories
            Categories.Clear();
            Categories.Add("All");
            var distinctCats = _allPrograms.Select(p => p.Category).Distinct().OrderBy(c => c);
            foreach (var cat in distinctCats) Categories.Add(cat);

            // Check Winget Status
            IsWingetInstalled = await _softwareService.IsWingetAvailableAsync();

            FilterPrograms();
        }

        private void FilterPrograms()
        {
            var filtered = _selectedCategory == "All" 
                ? _allPrograms 
                : _allPrograms.Where(p => p.Category == _selectedCategory).ToList();

            // Sort: Not Installed First, then Alphabetical
            var sortedList = filtered.OrderBy(p => p.IsInstalled).ThenBy(p => p.Name).ToList();

            Programs.Clear();
            foreach (var p in sortedList)
            {
                Programs.Add(p);
            }
        }

        private bool CanInstall() => !IsBusy && Programs.Any(p => p.IsSelected);

        private void ToggleSelection()
        {
            ((DelegateCommand)InstallCommand).RaiseCanExecuteChanged();
        }

        private async Task EnableWingetAsync()
        {
            if (IsBusy) return;
            
            IsBusy = true;
            StatusMessage = "Enabling Winget...";
            
            bool success = await _softwareService.EnableWingetAsync();
            
            IsWingetInstalled = success;
            IsBusy = false;
            
            if (success)
            {
                StatusMessage = "Winget enabled successfully.";
                await _alertService.ShowSuccessAsync("Winget Ready", "Winget has been enabled and is ready to use.");
            }
            else
            {
                StatusMessage = "Failed to enable Winget.";
                await _alertService.ShowErrorAsync("Enable Failed", "Could not enable Winget automatically. Please install 'App Installer' from Microsoft Store.");
            }
        }

        private async Task InstallSelectedAsync()
        {
            var selected = Programs.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0) return;

            IsBusy = true;
            StatusMessage = "Starting installation...";
            ProgressValue = 0;
            
            int total = selected.Count;
            int current = 0;

            foreach (var program in selected)
            {
                StatusMessage = $"Installing {program.Name}...";
                program.Status = "Installing...";
                program.IsInstalling = true;
                OnPropertyChanged(nameof(Programs)); // Refresh UI row if needed, though binding usually handles properies

                bool success = await _softwareService.InstallProgramAsync(program);

                program.IsInstalling = false;
                program.IsInstalled = success;
                program.Status = success ? "Installed" : "Failed";
                
                if (!success)
                {
                    await _logger.LogErrorAsync($"Failed to install {program.Name}", null);
                }

                current++;
                ProgressValue = (int)((double)current / total * 100);
            }

            StatusMessage = "Installation complete.";
            IsBusy = false;
            ((DelegateCommand)InstallCommand).RaiseCanExecuteChanged();

            await _alertService.ShowSuccessAsync("Installation Complete", $"Process finished. {selected.Count(p => p.IsInstalled)} programs installed successfully.");
        }

        private async Task UninstallProgramAsync(ProgramModel program)
        {
            if (program == null || !program.IsInstalled) return;

            // Confirm with user
            var confirmed = await _alertService.ShowConfirmAsync(
                "Program Kaldır",
                $"{program.Name} kaldırılacak (winget) ve programın kaldırma kaydı temizlenecek. " +
                "Kullanıcı verileri silinmez. Devam edilsin mi?");
            
            if (!confirmed) return;

            IsBusy = true;
            StatusMessage = $"Uninstalling {program.Name}...";
            program.Status = "Uninstalling...";
            program.IsInstalling = true;

            bool success = await _softwareService.UninstallProgramAsync(program);

            program.IsInstalling = false;
            
            if (success)
            {
                program.IsInstalled = false;
                program.Status = "Ready";
                StatusMessage = $"{program.Name} uninstalled successfully.";
                
                // Refresh the list to re-sort
                FilterPrograms();
                
                await _alertService.ShowSuccessAsync("Uninstall Complete", $"{program.Name} has been completely removed from your system.");
            }
            else
            {
                program.Status = "Uninstall Failed";
                StatusMessage = $"Failed to uninstall {program.Name}.";
                await _alertService.ShowErrorAsync("Uninstall Failed", $"Could not uninstall {program.Name}. Check logs for details.");
            }

            IsBusy = false;
        }
    }
}
