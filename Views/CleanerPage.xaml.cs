using System.Windows.Controls;
using RbnSystemPro.ViewModels;

namespace RbnSystemPro.Views
{
    public partial class CleanerPage : Page
    {
        public CleanerViewModel ViewModel { get; }

        public CleanerPage(CleanerViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
