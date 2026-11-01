using System.Windows.Controls;
using Wpf.Ui.Controls;
using RbnSystemPro.ViewModels;

namespace RbnSystemPro.Views
{
    public partial class ProgramsPage : Page
    {
        public ProgramsViewModel ViewModel { get; }

        public ProgramsPage(ProgramsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
