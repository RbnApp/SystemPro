using System;

namespace RbnSystemPro.Models
{
    public class SystemHealthModel
    {
        // Update Info
        public bool IsUpToDate { get; set; } = true;
        public string LastUpdateCheck { get; set; } = "Never";
        public int PendingUpdatesCount { get; set; } = 0;

        // Security Info
        public bool IsDefenderActive { get; set; } = false;
        public bool IsFirewallActive { get; set; } = false;
        public bool IsUacEnabled { get; set; } = false;
        public bool IsTpmEnabled { get; set; } = false;
        public bool IsSecureBootEnabled { get; set; } = false;
    }
}
