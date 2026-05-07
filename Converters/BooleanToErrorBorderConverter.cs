using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte un valor booleano en un pincel (<see cref="IBrush"/>) para el borde de una notificación.
    /// </summary>
    public class BooleanToErrorBorderConverter : IValueConverter
    {
        /// <summary>
        /// Evalúa la criticidad del error para devolver el color del borde correspondiente.
        /// </summary>
        /// <param name="value">Booleano que representa la criticidad.</param>
        /// <param name="targetType">Tipo de destino esperado.</param>
        /// <param name="parameter">Parámetro de enlace opcional.</param>
        /// <param name="culture">Cultura actual.</param>
        /// <returns>Un pincel <see cref="Brushes.Red"/> si es crítico; <see cref="Brushes.Orange"/> en caso contrario.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value is bool isCritical && isCritical)
                ? Brushes.Red
                : Brushes.Orange;
        }

        /// <summary>
        /// No implementado.
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}