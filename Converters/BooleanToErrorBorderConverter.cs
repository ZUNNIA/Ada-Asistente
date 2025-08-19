using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte un valor booleano a un Brush de borde para indicar un estado de error o advertencia en Avalonia.
    /// </summary>
    public class BooleanToErrorBorderConverter : IValueConverter
    {
        /// <summary>
        /// Convierte un booleano a un IBrush para el borde.
        /// </summary>
        /// <returns>Un Brush rojo si el valor es true; de lo contrario, un Brush naranja.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value is bool isCritical && isCritical)
                ? Brushes.Red
                : Brushes.Orange;
        }

        /// <summary>
        /// La conversión inversa no está implementada.
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
