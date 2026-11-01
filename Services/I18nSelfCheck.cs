using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RbnSystemPro.Services
{
    /// <summary>
    /// Duman testi için çalışma zamanı denetimi: iki sözlük gerçekten yükleniyor mu,
    /// anahtar kümeleri eşit mi, boş metin var mı. Çıktı dosyaya yazılır (WinExe'de konsol yok).
    /// Kullanım: RbnSystemPro.exe --i18n-check &lt;rapor yolu&gt;
    /// </summary>
    public static class I18nSelfCheck
    {
        public static void Run(string reportPath)
        {
            var sb = new StringBuilder();
            var (enKeys, trKeys) = Loc.Keys();
            var en = new HashSet<string>(enKeys, StringComparer.Ordinal);
            var tr = new HashSet<string>(trKeys, StringComparer.Ordinal);

            var onlyEn = en.Except(tr).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var onlyTr = tr.Except(en).OrderBy(x => x, StringComparer.Ordinal).ToList();

            var empty = new List<string>();
            var dictEn = Loc.DictionaryOf("en");
            var dictTr = Loc.DictionaryOf("tr");
            foreach (var key in en.Intersect(tr))
            {
                if (string.IsNullOrWhiteSpace(dictEn[key]) || string.IsNullOrWhiteSpace(dictTr[key]))
                {
                    empty.Add(key);
                }
            }

            var ok = onlyEn.Count == 0 && onlyTr.Count == 0 && empty.Count == 0 && en.Count > 0;

            sb.AppendLine("RbnSystemPro localization self-check");
            sb.AppendLine($"en keys : {en.Count}");
            sb.AppendLine($"tr keys : {tr.Count}");
            sb.AppendLine($"only in en : {onlyEn.Count}{(onlyEn.Count > 0 ? " -> " + string.Join(", ", onlyEn) : "")}");
            sb.AppendLine($"only in tr : {onlyTr.Count}{(onlyTr.Count > 0 ? " -> " + string.Join(", ", onlyTr) : "")}");
            sb.AppendLine($"empty value: {empty.Count}{(empty.Count > 0 ? " -> " + string.Join(", ", empty) : "")}");
            sb.AppendLine($"missing-key fallback sample (tr): {Loc.T("this.key.does.not.exist")}");
            sb.AppendLine(ok ? "RESULT: OK" : "RESULT: FAIL");

            try
            {
                File.WriteAllText(reportPath, sb.ToString(), new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RbnSystemPro] i18n raporu yazılamadı: {ex.Message}");
                ok = false;
            }

            Environment.ExitCode = ok ? 0 : 1;
        }
    }
}
