using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Turbix.Controls
{
    /// <summary>bool → Visible/Collapsed. Con ConverterParameter="invert" invierte el resultado.</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool v && v;
            if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase)) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Texto en mayúsculas (encabezados de sección).</summary>
    public sealed class UpperCaseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value as string)?.ToUpper(culture);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
