using System.Globalization;
using System.Windows.Data;

namespace FacturacionArca.Wpf;

/// <summary>
/// Converter para usar enums en RadioButton (IsChecked).
/// Bind con ConverterParameter="NombreDelValorEnum".
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is not null)
            return Enum.Parse(targetType, parameter.ToString()!);
        return Binding.DoNothing;
    }
}
