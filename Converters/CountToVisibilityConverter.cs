using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Evalúa si un recuento entero es mayor a cero para determinar la visibilidad de un control.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Convierte un entero en un booleano de visibilidad.
        /// </summary>
        /// <param name="value">El número de elementos (int).</param>
        /// <param name="targetType">Tipo esperado.</param>
        /// <param name="parameter">Parámetro opcional.</param>
        /// <param name="culture">Cultura.</param>
        /// <returns>True si el valor es mayor a 0; de lo contrario, False.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is int count && count > 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}