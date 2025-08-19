using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte un recuento numérico (integer) a un valor booleano para controlar la visibilidad en Avalonia.
    /// Si el recuento es mayor que cero, el resultado es true (Visible); de lo contrario, es false (Collapsed).
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Convierte un recuento entero a un valor booleano.
        /// </summary>
        /// <param name="value">El recuento de elementos (debe ser un entero).</param>
        /// <returns>True si el recuento es > 0; de lo contrario, false.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value is int count && count > 0);
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
