using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RbnSystemPro.Services;

namespace RbnSystemPro.Helpers
{
    public class BooleanToActivationBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActivated && isActivated)
                return Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"];
            
            return Application.Current.Resources["SystemFillColorCriticalBackgroundBrush"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToActivationTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Loc.T((value is bool isActivated && isActivated) ? "hw.license.active" : "hw.license.inactive");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isOk && isOk) ? "ShieldCheckmark24" : "ShieldError24";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b) ? !b : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b) ? !b : value;
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EventTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RbnSystemPro.Models.TimelineEventType type)
            {
                return type switch
                {
                    RbnSystemPro.Models.TimelineEventType.Success => "CheckmarkCircle24",
                    RbnSystemPro.Models.TimelineEventType.Error => "DismissCircle24",
                    RbnSystemPro.Models.TimelineEventType.Warning => "Warning24",
                    RbnSystemPro.Models.TimelineEventType.Installation => "ArrowDownload24",
                    RbnSystemPro.Models.TimelineEventType.Optimization => "Wrench24",
                    _ => "Info24"
                };
            }
            return "Info24";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EventTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RbnSystemPro.Models.TimelineEventType type)
            {
                return type switch
                {
                    RbnSystemPro.Models.TimelineEventType.Success => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745")),
                    RbnSystemPro.Models.TimelineEventType.Error => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")),
                    RbnSystemPro.Models.TimelineEventType.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")),
                    RbnSystemPro.Models.TimelineEventType.Installation => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007BFF")),
                    RbnSystemPro.Models.TimelineEventType.Optimization => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6F42C1")),
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17A2B8"))
                };
            }
            return Brushes.Gray;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status && status == "Ready")
            {
                return Brushes.White; // Better contrast for green background
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ConnectionTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string type && type == "WiFi") return "WifiSettings24";
            if (value is string type2 && type2 == "LAN") return "Connector24";
            return "CellularDataOff24";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && count > 0) return Visibility.Visible;
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
