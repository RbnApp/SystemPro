using System;
using Microsoft.Win32;

namespace RbnSystemPro.Services
{
    /// <summary>
    /// Kaldırma sonrası "derin temizlik" — YALNIZ kayıt defterindeki kaldırma kayıtları.
    ///
    /// Eski davranış (denetim bulgusu #3) program adının geçtiği HER klasörü ve HER kayıt
    /// anahtarını siliyordu: "Chrome" adlı bir programı kaldırmak, adında "Chrome" geçen
    /// başka bir klasörü/anahtarı da götürebiliyordu. Artık:
    ///   • hiçbir klasör silinmez (dosyaları programın kendi kaldırıcısı/winget yönetir),
    ///   • anahtar yalnız TAM eşleşirse silinir: anahtar adı = program kimliği ya da
    ///     DisplayName = program adı (büyük/küçük harf duyarsız, "içeren" eşleşme YOK).
    /// </summary>
    public static class UninstallCleanup
    {
        public static readonly string[] UninstallKeyPaths =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        public static bool IsExactMatch(string keyName, string? displayName, string programId, string programName)
        {
            if (!string.IsNullOrWhiteSpace(programId) &&
                keyName.Equals(programId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(programName) &&
                !string.IsNullOrWhiteSpace(displayName) &&
                displayName.Trim().Equals(programName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>Verilen kök/path altında TAM eşleşen kaldırma anahtarlarını siler. Silinen sayısı döner.</summary>
        public static int DeleteExactUninstallKeys(RegistryKey root, string uninstallPath, string programId, string programName, ILoggerService logger)
        {
            int removed = 0;

            try
            {
                using var key = root.OpenSubKey(uninstallPath, writable: true);
                if (key == null) return 0;

                foreach (var subkeyName in key.GetSubKeyNames())
                {
                    string? displayName = null;
                    using (var subkey = key.OpenSubKey(subkeyName))
                    {
                        displayName = subkey?.GetValue("DisplayName")?.ToString();
                    }

                    if (!IsExactMatch(subkeyName, displayName, programId, programName)) continue;

                    try
                    {
                        key.DeleteSubKeyTree(subkeyName, throwOnMissingSubKey: false);
                        removed++;
                        _ = logger.LogInfoAsync($"Kaldırma kaydı silindi: {uninstallPath}\\{subkeyName}");
                    }
                    catch (Exception ex)
                    {
                        _ = logger.LogWarningAsync($"Kaldırma kaydı silinemedi ({subkeyName}): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _ = logger.LogWarningAsync($"Kaldırma kayıtları okunamadı ({uninstallPath}): {ex.Message}");
            }

            return removed;
        }

        /// <summary>HKLM (64/32 bit) + HKCU altındaki tam eşleşen kayıtları siler. Silinen sayısı döner.</summary>
        public static int CleanUninstallRegistry(string programId, string programName, ILoggerService logger)
        {
            int removed = 0;

            foreach (var path in UninstallKeyPaths)
            {
                removed += DeleteExactUninstallKeys(Registry.LocalMachine, path, programId, programName, logger);
            }

            removed += DeleteExactUninstallKeys(Registry.CurrentUser, UninstallKeyPaths[0], programId, programName, logger);

            return removed;
        }
    }
}
