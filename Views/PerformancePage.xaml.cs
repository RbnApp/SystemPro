using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RbnSystemPro.Services;
using RbnSystemPro.Models;

namespace RbnSystemPro.Views
{
    public partial class PerformancePage : Page, INotifyPropertyChanged
    {
        private readonly IPerformanceService _performanceService;
        private PerformanceModel _performance = new();
        private DiskDriveInfo? _selectedDisk;
        private DispatcherTimer _timer;

        public PerformanceModel Performance
        {
            get => _performance;
            set { _performance = value; OnPropertyChanged(); }
        }

        public DiskDriveInfo? SelectedDisk
        {
            get => _selectedDisk;
            set { _selectedDisk = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDetailViewActive)); }
        }

        public bool IsDetailViewActive => SelectedDisk != null;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PerformancePage(IPerformanceService performanceService)
        {
            InitializeComponent();
            _performanceService = performanceService;
            this.DataContext = this;

            // Denetim H1: 2 s'lik döngü tek çekirdeğin ~%2,5'ini yiyordu. 5 s yeterli
            // (değerler zaten "anlık" değil, birkaç saniyelik ortalamalar).
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _timer.Tick += async (s, e) => await LoadDataAsync();
            
            this.Loaded += (s, e) =>
            {
                _ = LoadDataAsync();
                _timer.Start();
            };

            // Stop timer when page is unloaded to save resources
            this.Unloaded += (s, e) => _timer.Stop();
        }

        private void OnDiskCardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is DiskDriveInfo disk)
            {
                if (disk.IsCloudDrive) return;

                SelectedDisk = disk;
            }
        }

        private void OnBackToListClick(object sender, RoutedEventArgs e)
        {
            SelectedDisk = null;
        }

        private bool _isLoading;

        private async Task LoadDataAsync()
        {
            // Tek uçuş (re-entry) kilidi: WMI turu uzarsa bir sonraki tick üst üste binmez,
            // aksi halde yavaş makinede çağrılar birikip CPU'yu katlar (denetim H1).
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                var data = await _performanceService.GetPerformanceDataAsync();
                Performance = data;

                if (SelectedDisk != null)
                {
                    var updatedDisk = data.Disks?.FirstOrDefault(d => d.Name == SelectedDisk.Name);
                    if (updatedDisk != null)
                    {
                        SelectedDisk = updatedDisk;
                    }
                }
            }
            catch (Exception ex)
            {
                // Silent fail or log - don't crash the UI thread
                System.Diagnostics.Debug.WriteLine($"Performance Load Error: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
