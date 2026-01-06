using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SteamStatusChanger.Common
{
    public class ColorStringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s))
            {
                return new SolidColorBrush(Colors.Transparent);
            }

            try
            {
                var col = (Color)ColorConverter.ConvertFromString(s);
                return new SolidColorBrush(col);
            }
            catch
            {
                return new SolidColorBrush(Colors.Transparent);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush b)
            {
                var c = b.Color;
                return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            }

            return "";
        }
    }
}
