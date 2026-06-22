using System.Globalization;
using System.Windows.Data;

namespace FacturacionArca.Wpf;

public sealed class BooleanInverter : IValueConverter
{
    public static readonly BooleanInverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}
