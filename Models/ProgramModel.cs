using RbnSystemPro.Helpers;

namespace RbnSystemPro.Models
{
    public class ProgramModel : ObservableObject
    {
        private bool _isSelected;
        private string _statusKey = "status.ready";
        private bool _isInstalling;
        private bool _isInstalled;

        public string Id { get; set; } = string.Empty; // Winget ID
        public string Name { get; set; } = string.Empty;

        // DAPP-41: çevrilecek metinler anahtar olarak tutulur; ekran LocKeyConverter ile çizer.
        public string DescriptionKey { get; set; } = string.Empty;
        public string IconSymbol { get; set; } = "Apps24";
        public string HexColor { get; set; } = "#4F4F4F"; // Default neutral color
        public string CategoryKey { get; set; } = "cat.all";

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Status tracking
        public string StatusKey
        {
            get => _statusKey;
            set => SetProperty(ref _statusKey, value);
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
