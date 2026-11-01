using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RbnSystemPro.Services
{
    /// <summary>
    /// Wi-Fi SSID doğrulaması ve netsh çağrılarının güvenli kurulumu.
    ///
    /// Kural: SSID (ve profil adı) HİÇBİR ZAMAN bir kabuk komut dizesine eklenmez
    /// (eski kod <c>cmd.exe /c netsh wlan connect name="..."</c> kullanıyordu; SSID içine
    /// tırnak ve &lt; &amp; gibi karakterler yazılarak komut çalıştırılabiliyordu).
    /// Argümanlar <see cref="ProcessStartInfo.ArgumentList"/> ile ayrı ayrı verilir,
    /// böylece kabuk devre dışı kalır.
    /// </summary>
    public static class WifiProfileHelper
    {
        // Windows SSID'leri 1-32 karakterdir; harf/rakam (Unicode dahil), boşluk, tire ve nokta.
        private static readonly Regex SsidPattern = new(@"^[\w \-\.]{1,32}$", RegexOptions.Compiled);

        public static bool IsValidSsid(string? ssid)
        {
            return !string.IsNullOrEmpty(ssid) && SsidPattern.IsMatch(ssid);
        }

        /// <summary>
        /// Profil XML'ini geçici dosyaya yazar.
        ///
        /// Not: netsh, DeleteOnClose ile açılmış bir dosyayı AÇAMAZ (ölçüldü: "Windows cannot open
        /// the file named ..."). Bu yüzden dosya normal olarak yazılıp kapatılır, netsh çalıştıktan
        /// sonra <see cref="DeleteTemporaryProfile"/> ile silinir. Riski azaltmak için dosya yalnız
        /// geçerli kullanıcının erişebileceği bir ACL ile oluşturulur ve adı tahmin edilemez.
        /// </summary>
        public static void WriteTemporaryProfile(string path, string profileXml)
        {
            var options = new System.IO.FileStreamOptions
            {
                Mode = System.IO.FileMode.CreateNew,
                Access = System.IO.FileAccess.Write,
                Share = System.IO.FileShare.None
            };

            using (var stream = new System.IO.FileStream(path, options))
            {
                using var writer = new System.IO.StreamWriter(stream, new System.Text.UTF8Encoding(false));
                writer.Write(profileXml);
            }
        }

        /// <summary>Geçici profil dosyasını siler. true = dosya artık yok.</summary>
        public static bool DeleteTemporaryProfile(string path, out string? error)
        {
            error = null;
            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
                return !System.IO.File.Exists(path);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>netsh wlan add profile filename="...xml" — kabuğa dokunmadan.</summary>
        public static ProcessStartInfo BuildAddProfileStartInfo(string profilePath)
        {
            return BuildNetshStartInfo("wlan", "add", "profile", $"filename={profilePath}");
        }

        /// <summary>netsh wlan connect name="ssid" ssid="ssid"</summary>
        public static ProcessStartInfo BuildConnectStartInfo(string ssid)
        {
            return BuildNetshStartInfo("wlan", "connect", $"name={ssid}", $"ssid={ssid}");
        }

        /// <summary>netsh wlan show profile name="ssid" key=clear</summary>
        public static ProcessStartInfo BuildShowProfileStartInfo(string ssid)
        {
            return BuildNetshStartInfo("wlan", "show", "profile", $"name={ssid}", "key=clear");
        }

        private static ProcessStartInfo BuildNetshStartInfo(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("netsh")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }
    }
}
