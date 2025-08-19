using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte un valor booleano en un Brush de fondo específico para notificaciones en Avalonia.
    /// Se utiliza para cambiar el color de fondo de un control basado en si una notificación es crítica.
    /// </summary>
    public class BooleanToErrorBackgroundConverter : IValueConverter
    {
        /// <summary>
        /// Convierte un booleano a un IBrush.
        /// </summary>
        /// <returns>Un Brush rojo si el valor es true; de lo contrario, un Brush ámbar.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isCritical)
            {
                // Devuelve un color de fondo rojo o ámbar semi-transparente.
                return isCritical
                    ? new SolidColorBrush(Color.FromArgb(176, 255, 82, 82))  // #B0FF5252
                    : new SolidColorBrush(Color.FromArgb(176, 255, 193, 7)); // #B0FFC107
            }
            return Brushes.Transparent;
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
