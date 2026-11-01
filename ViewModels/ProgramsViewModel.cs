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
        // DAPP-41: durum metni anahtar + argüman olarak tutulur; dil değişince kendiliğinden çevrilir.
        private string _statusKey = "programs.status.select";
        private object?[] _statusArgs = System.Array.Empty<object?>();
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
        private string _selectedCategory = "cat.all";

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

        public string StatusMessage => Loc.T(_statusKey, _statusArgs);

        private void SetStatus(string key, params object?[] args)
        {
            _statusKey = key;
            _statusArgs = args;
            OnPropertyChanged(nameof(StatusMessage));
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
            Categories.Add("cat.all");
            var distinctCats = _allPrograms.Select(p => p.CategoryKey).Distinct().OrderBy(c => c);
            foreach (var cat in distinctCats) Categories.Add(cat);

            // Check Winget Status
            IsWingetInstalled = await _softwareService.IsWingetAvailableAsync();

            FilterPrograms();
        }

        private void FilterPrograms()
        {
            var filtered = _selectedCategory == "cat.all"
                ? _allPrograms
                : _allPrograms.Where(p => p.CategoryKey == _selectedCategory).ToList();

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
            SetStatus("programs.status.enablingWinget");
            
            bool success = await _softwareService.EnableWingetAsync();
            
            IsWingetInstalled = success;
            IsBusy = false;
            
            if (success)
            {
                SetStatus("programs.status.wingetEnabled");
                await _alertService.ShowSuccessAsync(Loc.T("programs.wingetReadyTitle"), Loc.T("programs.wingetReadyMsg"));
            }
            else
            {
                SetStatus("programs.status.wingetEnableFailed");
                await _alertService.ShowErrorAsync(Loc.T("programs.enableFailedTitle"), Loc.T("programs.enableFailedMsg"));
            }
        }

        private async Task InstallSelectedAsync()
        {
            var selected = Programs.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0) return;

            IsBusy = true;
            SetStatus("programs.status.startingInstall");
            ProgressValue = 0;
            
            int total = selected.Count;
            int current = 0;

            foreach (var program in selected)
            {
                SetStatus("programs.status.installing", program.Name);
                program.StatusKey = "status.installing";
                program.IsInstalling = true;
                OnPropertyChanged(nameof(Programs)); // Refresh UI row if needed, though binding usually handles properies

                bool success = await _softwareService.InstallProgramAsync(program);

                program.IsInstalling = false;
                program.IsInstalled = success;
                program.StatusKey = success ? "status.installed" : "status.failed";
                
                if (!success)
                {
                    await _logger.LogErrorAsync($"Failed to install {program.Name}", null);
                }

                current++;
                ProgressValue = (int)((double)current / total * 100);
            }

            SetStatus("programs.status.installComplete");
            IsBusy = false;
            ((DelegateCommand)InstallCommand).RaiseCanExecuteChanged();

            await _alertService.ShowSuccessAsync(Loc.T("programs.installCompleteTitle"), Loc.T("programs.installCompleteMsg", selected.Count(p => p.IsInstalled)));
        }

        private async Task UninstallProgramAsync(ProgramModel program)
        {
            if (program == null || !program.IsInstalled) return;

            // Confirm with user
            var confirmed = await _alertService.ShowConfirmAsync(
                Loc.T("programs.uninstallConfirmTitle"),
                Loc.T("programs.uninstallConfirmMsg", program.Name));
            
            if (!confirmed) return;

            IsBusy = true;
            SetStatus("programs.status.uninstalling", program.Name);
            program.StatusKey = "status.uninstalling";
            program.IsInstalling = true;

            bool success = await _softwareService.UninstallProgramAsync(program);

            program.IsInstalling = false;
            
            if (success)
            {
                program.IsInstalled = false;
                program.StatusKey = "status.ready";
                SetStatus("programs.status.uninstalled", program.Name);
                
                // Refresh the list to re-sort
                FilterPrograms();
                
                await _alertService.ShowSuccessAsync(Loc.T("programs.uninstallCompleteTitle"), Loc.T("programs.uninstallCompleteMsg", program.Name));
            }
            else
            {
                program.StatusKey = "status.uninstallFailed";
                SetStatus("programs.status.uninstallFailed", program.Name);
                await _alertService.ShowErrorAsync(Loc.T("programs.uninstallFailedTitle"), Loc.T("programs.uninstallFailedMsg", program.Name));
            }

            IsBusy = false;
        }
    }
}
