using System.Windows.Controls;
using RbnSystemPro.Services;
using System.Collections.Specialized;
using System.Windows;

namespace RbnSystemPro.Views
{
    public partial class TimelinePage : Page
    {
        private readonly ITimelineService _timelineService;

        public TimelinePage(ITimelineService timelineService)
        {
            InitializeComponent();
            _timelineService = timelineService;
            
            TimelineItems.ItemsSource = _timelineService.Events;
            
            // Initial check
            UpdateEmptyStateVisibility();
            
            // Subscribe to changes to update empty state
            _timelineService.Events.CollectionChanged += Events_CollectionChanged;
        }

        private void Events_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateEmptyStateVisibility();
        }

        private void UpdateEmptyStateVisibility()
        {
            if (EmptyStatePanel != null)
            {
                EmptyStatePanel.Visibility = (_timelineService.Events.Count == 0) 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }
    }
}
