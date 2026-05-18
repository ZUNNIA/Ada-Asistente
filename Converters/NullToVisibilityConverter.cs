using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte la existencia de un objeto (null o no null) en un valor booleano de visibilidad.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Evalúa si un objeto es nulo.
        /// </summary>
        /// <param name="value">El objeto a evaluar.</param>
        /// <param name="targetType">Tipo de destino.</param>
        /// <param name="parameter">Si se pasa "VisibleIfNull", la lógica se invierte.</param>
        /// <param name="culture">Cultura.</param>
        /// <returns>True si el objeto no es nulo (o si es nulo y se usa el parámetro de inversión).</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool visibleIfNull = string.Equals(parameter as string, "VisibleIfNull", StringComparison.OrdinalIgnoreCase);

            return visibleIfNull ? isNull : !isNull;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}