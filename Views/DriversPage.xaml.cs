using System.Windows.Controls;
using RbnSystemPro.ViewModels;

namespace RbnSystemPro.Views
{
    public partial class DriversPage : Page
    {
        public DriversViewModel ViewModel { get; }

        public DriversPage(DriversViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
