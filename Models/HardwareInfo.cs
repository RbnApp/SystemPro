using RbnSystemPro.Services;

namespace RbnSystemPro.Models
{
    /// <summary>
    /// Donanım bilgisi. DAPP-41: tarama öncesi yer tutucu metinler anahtarla çevrilir
    /// (dil değişince ekran yeniden çizer, değer gelmişse değer gösterilir).
    /// </summary>
    public class HardwareInfo
    {
        private string _cpuName = string.Empty;
        private string _motherboard = string.Empty;
        private string _gpuName = string.Empty;
        private double _gpuDedicatedGb = -1;
        private string _biosVersion = string.Empty;
        private string _biosDate = string.Empty;
        private string _windowsEdition = string.Empty;
        private string _biosStatus = string.Empty;
        private string _biosStatusKey = "common.unknown";

        public string CpuName { get => Placeholder(_cpuName, "hw.unknownCpu"); set => _cpuName = value; }
        public bool HasCpuName => !string.IsNullOrEmpty(_cpuName);
        public string RamTotal { get; set; } = "0 GB";
        public string Motherboard { get => Placeholder(_motherboard, "hw.unknownMb"); set => _motherboard = value; }
        public bool HasMotherboard => !string.IsNullOrEmpty(_motherboard);
        public string GpuName { get => Placeholder(_gpuName, "hw.unknownGpu"); set => _gpuName = value; }
        public bool HasGpuName => !string.IsNullOrEmpty(_gpuName);
        // DAPP-41: sayılar saklanır, metin okuma anında çevrilir (dil değişince de doğru kalır).
        public double GpuDedicatedGb { get => _gpuDedicatedGb; set => _gpuDedicatedGb = value; }
        public double GpuSharedGb { get; set; }
        public double GpuTotalGb { get; set; }
        public bool HasGpuMemory => _gpuDedicatedGb >= 0;
        public string GpuMemory => HasGpuMemory
            ? Loc.T("hw.gpuMemoryFormat", GpuDedicatedGb, GpuSharedGb, GpuTotalGb)
            : Loc.T("hw.unknownVram");
        public string BiosVersion { get => Placeholder(_biosVersion, "common.unknown"); set => _biosVersion = value; }
        public string BiosDate { get => Placeholder(_biosDate, "common.unknown"); set => _biosDate = value; }
        public double CpuTemperature { get; set; }
        public int NumberOfCores { get; set; }
        public int NumberOfLogicalProcessors { get; set; }
        public System.Collections.Generic.List<string> RamSlots { get; set; } = new();
        public bool IsWindowsActivated { get; set; }
        public string WindowsEdition { get => Placeholder(_windowsEdition, "common.unknown"); set => _windowsEdition = value; }

        // BIOS Update
        /// <summary>Durum metni; anahtarla verildiyse (BiosStatusKey) çevrilir.</summary>
        public string BiosStatus
        {
            get => string.IsNullOrEmpty(_biosStatus)
                ? Loc.T(string.IsNullOrEmpty(_biosStatusKey) ? "common.unknown" : _biosStatusKey)
                : _biosStatus;
            set { _biosStatus = value; _biosStatusKey = string.Empty; }
        }

        /// <summary>BiosStatus için sözlük anahtarı (ör. "hw.onlineCheck").</summary>
        public string BiosStatusKey
        {
            set { _biosStatusKey = value; _biosStatus = string.Empty; }
        }

        public string BiosUpdateUrl { get; set; } = string.Empty;
        public bool IsBiosUpdateAvailable { get; set; } = false;

        private static string Placeholder(string value, string key)
            => string.IsNullOrEmpty(value) ? Loc.T(key) : value;
    }
}
