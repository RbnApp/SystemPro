using System;
using RbnSystemPro.Services;

namespace RbnSystemPro.Models
{
    public class SystemHealthModel
    {
        // Update Info
        public bool IsUpToDate { get; set; } = true;
        private string _lastUpdateCheck = string.Empty;

        // DAPP-41: "hiç" durumu anahtarla çevrilir.
        public string LastUpdateCheck
        {
            get => string.IsNullOrWhiteSpace(_lastUpdateCheck) ? Loc.T("security.lastUpdate.never") : _lastUpdateCheck;
            set => _lastUpdateCheck = value;
        }
        public int PendingUpdatesCount { get; set; } = 0;

        // Security Info
        public bool IsDefenderActive { get; set; } = false;
        public bool IsFirewallActive { get; set; } = false;
        public bool IsUacEnabled { get; set; } = false;
        public bool IsTpmEnabled { get; set; } = false;
        public bool IsSecureBootEnabled { get; set; } = false;
    }
}
