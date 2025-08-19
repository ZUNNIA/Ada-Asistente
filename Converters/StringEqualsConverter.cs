using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Compara un valor de cadena con un parámetro y devuelve true si son iguales (ignorando mayúsculas y minúsculas).
    /// Se utiliza en XAML para aplicar pseudoclases condicionalmente.
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value as string)?.Equals(parameter as string, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
