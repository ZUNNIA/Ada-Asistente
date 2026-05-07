using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte un valor booleano en un pincel (<see cref="IBrush"/>) de fondo para notificaciones.
    /// Se utiliza para diferenciar visualmente errores críticos de advertencias.
    /// </summary>
    public class BooleanToErrorBackgroundConverter : IValueConverter
    {
        /// <summary>
        /// Realiza la conversión de un estado de criticidad a un color de fondo.
        /// </summary>
        /// <param name="value">Un valor booleano que indica si el error es crítico (true) o una advertencia (false).</param>
        /// <param name="targetType">El tipo de la propiedad de destino (debe ser <see cref="IBrush"/>).</param>
        /// <param name="parameter">Parámetro opcional pasado desde XAML (no se utiliza).</param>
        /// <param name="culture">Información de cultura para la conversión (no se utiliza).</param>
        /// <returns>Un <see cref="SolidColorBrush"/> rojo suave si es crítico; de lo contrario, un tono ámbar.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isCritical)
            {
                return isCritical
                    ? new SolidColorBrush(Color.FromArgb(176, 255, 82, 82))  // Rojo (#B0FF5252)
                    : new SolidColorBrush(Color.FromArgb(176, 255, 193, 7)); // Ámbar (#B0FFC107)
            }
            return Brushes.Transparent;
        }

        /// <summary>
        /// La conversión inversa no está soportada para este componente visual.
        /// </summary>
        /// <exception cref="NotSupportedException">Se lanza siempre que se intente usar en un Binding TwoWay.</exception>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("BooleanToErrorBackgroundConverter solo admite conversiones de ida (OneWay).");
        }
    }
}