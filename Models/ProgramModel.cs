using RbnSystemPro.Helpers;

namespace RbnSystemPro.Models
{
    public class ProgramModel : ObservableObject
    {
        private bool _isSelected;
        private string _status = "Ready";
        private bool _isInstalling;
        private bool _isInstalled;

        public string Id { get; set; } = string.Empty; // Winget ID
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconSymbol { get; set; } = "Apps24"; 
        public string HexColor { get; set; } = "#4F4F4F"; // Default neutral color 
        public string Category { get; set; } = "General"; 

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        
        // Status tracking
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsInstalling
        {
            get => _isInstalling;
            set => SetProperty(ref _isInstalling, value);
        }

        public bool IsInstalled
        {
            get => _isInstalled;
            set => SetProperty(ref _isInstalled, value);
        }
    }
}
