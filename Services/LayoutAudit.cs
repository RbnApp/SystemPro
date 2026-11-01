using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RbnSystemPro.Services
{
    /// <summary>
    /// DAPP-41 doğrulama aracı: <c>--layout-audit &lt;rapor&gt;</c> ile açıldığında her sayfa
    /// çizildikten sonra görünür metin kutularını ölçer ve taşan/kırpılan metni yazar.
    /// Normal kullanımda etkisizdir (bayrak verilmezse hiçbir şey yapmaz).
    /// </summary>
    public static class LayoutAudit
    {
        public static bool Enabled { get; private set; }
        private static string _reportPath = string.Empty;
        private static readonly List<string> _buffer = new();
        private static int _pageCount;

        public static void Configure(string reportPath)
        {
            _reportPath = reportPath;
            Enabled = true;
        }

        /// <summary>Çerçeveye bağlanır: her başarılı gezinmeden sonra sayfa denetlenir.</summary>
        public static void Attach(Frame frame)
        {
            if (!Enabled) return;

            frame.Navigated += (_, _) =>
            {
                // Veri (WMI, sürücü listesi) asenkron yüklenir; yerleşim oturduktan sonra ölç.
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (frame.Content is FrameworkElement page) Audit(page);
                };
                timer.Start();
            };
        }

        private static void Audit(FrameworkElement page)
        {
            var pageName = page.GetType().Name;
            var issues = new List<string>();
            var dpi = VisualTreeHelper.GetDpi(page);

            foreach (var tb in Descendants<TextBlock>(page))
            {
                if (!tb.IsVisible) continue;
                var text = tb.Text;
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (tb.ActualWidth <= 0 || tb.ActualHeight <= 0) continue;

                var typeface = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch);
                var ft = new FormattedText(
                    text,
                    CultureInfo.CurrentUICulture,
                    tb.FlowDirection,
                    typeface,
                    tb.FontSize,
                    Brushes.Black,
                    dpi.PixelsPerDip);

                var availableWidth = tb.ActualWidth - tb.Padding.Left - tb.Padding.Right;
                if (availableWidth <= 0) continue;

                if (tb.TextWrapping == TextWrapping.NoWrap)
                {
                    if (ft.WidthIncludingTrailingWhitespace > availableWidth + 2)
                    {
                        issues.Add($"  YATAY TAŞMA/{pageName}: \"{Shorten(text)}\" gerekli {ft.WidthIncludingTrailingWhitespace:F0}px / mevcut {availableWidth:F0}px");
                    }
                }
                else
                {
                    ft.MaxTextWidth = availableWidth;
                    var availableHeight = tb.ActualHeight - tb.Padding.Top - tb.Padding.Bottom;
                    if (ft.Height > availableHeight + 2)
                    {
                        issues.Add($"  DIKEY TAŞMA/{pageName}: \"{Shorten(text)}\" gerekli {ft.Height:F0}px / mevcut {availableHeight:F0}px");
                    }
                }
            }

            _pageCount++;
            var sb = new StringBuilder();
            sb.AppendLine($"[sayfa {_pageCount}] {pageName} — {(issues.Count == 0 ? "taşma yok" : issues.Count + " bulgu")}");
            foreach (var i in issues) sb.AppendLine(i);
            _buffer.Add(sb.ToString());
            Write();
        }

        private static string Shorten(string s) => s.Length <= 60 ? s : s[..57] + "...";

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var d in Descendants<T>(child)) yield return d;
            }
        }

        private static void Write()
        {
            try
            {
                File.WriteAllText(_reportPath, string.Join(Environment.NewLine, _buffer), Encoding.UTF8);
            }
            catch
            {
                // rapor yazılamazsa uygulama akışı bozulmaz
            }
        }
    }
}
