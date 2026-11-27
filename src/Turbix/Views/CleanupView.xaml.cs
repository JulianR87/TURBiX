using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using Turbix.Localization;

namespace Turbix.Views
{
    public partial class CleanupView : UserControl
    {
        public CleanupView()
        {
            InitializeComponent();
        }
    }

    /// <summary>Número con su texto en singular o plural: ConverterParameter es la clave del plural (L.P).</summary>
    public sealed class CleanupPluralConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long n = value is IConvertible c ? c.ToInt64(CultureInfo.InvariantCulture) : 0;
            return L.P(parameter as string, n);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
