using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte un valor booleano a su valor booleano inverso para controlar la visibilidad en Avalonia.
    /// true -> false (oculto)
    /// false -> true (visible)
    /// </summary>
    public class BooleanToVisibilityConverterInverted : IValueConverter
    {
        /// <summary>
        /// Convierte un booleano a su valor opuesto.
        /// </summary>
        /// <returns>False si el valor es true; de lo contrario, true.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !(value is bool boolValue && boolValue);
        }

        /// <summary>
        /// Convierte un booleano a su valor opuesto.
        /// </summary>
        /// <returns>False si el valor es true; de lo contrario, true.</returns>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !(value is bool boolValue && boolValue);
        }
    }
}
