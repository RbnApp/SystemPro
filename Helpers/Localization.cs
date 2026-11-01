using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using RbnSystemPro.Services;

namespace RbnSystemPro.Helpers
{
    /// <summary>
    /// XAML'de metin anahtarının TEK kullanım biçimi: <c>Text="{l:T dash.title}"</c>.
    /// Bir DynamicResource bağı üretir; dil değişince (sözlük değişince) metin yeniden
    /// başlatma olmadan güncellenir.
    /// </summary>
    [MarkupExtensionReturnType(typeof(object))]
    public class TExtension : MarkupExtension
    {
        public TExtension()
        {
        }

        public TExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return string.Empty;

            // DynamicResource: anahtar uygulama kaynaklarında aranır, sözlük değişince tazelenir.
            return new DynamicResourceExtension(Key).ProvideValue(serviceProvider);
        }
    }

    /// <summary>
    /// Şablon (DataTemplate / ControlTemplate) içinde kullanılan biçim: <c>Text="{l:B key}"</c>.
    /// DynamicResource şablon içinde XamlParseException üretir; bu uzantı tekil bir kaynağa
    /// bağlanan <see cref="Binding"/> döndürür, dil değişince tazelenir ve şablonlarda güvenlidir.
    /// </summary>
    [MarkupExtensionReturnType(typeof(Binding))]
    public class BExtension : MarkupExtension
    {
        public BExtension()
        {
        }

        public BExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return string.Empty;
            return new Binding("[" + Key + "]")
            {
                Source = LocSource.Instance,
                Mode = BindingMode.OneWay,
            };
        }
    }

    /// <summary>
    /// Şablon bağlarının okuduğu tekil metin kaynağı: <c>LocSource.Instance["anahtar"]</c>.
    /// Dil değişince tek bir PropertyChanged ile tüm bağlar tazelenir.
    /// </summary>
    public sealed class LocSource : System.ComponentModel.INotifyPropertyChanged
    {
        public static LocSource Instance { get; } = new LocSource();

        private LocSource()
        {
            Loc.LanguageChanged += (_, _) => Refresh();
        }

        public string this[string key] => Loc.T(key);

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public void Refresh() => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
    }

    /// <summary>Modelde tutulan anahtarı (ör. StatusKey, DescriptionKey) ekranda metne çevirir.</summary>
    public class LocKeyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is string key && !string.IsNullOrEmpty(key) ? Loc.T(key) : string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Biçim metnini de çeviren dönüştürücü: <c>{Binding X, Converter=..., ConverterParameter=cleaner.issues}</c>
    /// → ConverterParameter anahtarının metni {0} ile değere uygulanır.
    /// </summary>
    public class LocFormatConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is not string key || string.IsNullOrEmpty(key)) return value?.ToString() ?? string.Empty;
            return Loc.T(key, value);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
