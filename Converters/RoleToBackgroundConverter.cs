using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Asigna un color de fondo (Brush) a la burbuja de mensaje según el rol del remitente.
    /// </summary>
    public class RoleToBackgroundConverter : IValueConverter
    {
        private static readonly IBrush UserBrush = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)); // #1AFFFFFF
        private static readonly IBrush AssistantBrush = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)); // #10FFFFFF

        /// <summary>
        /// Convierte el rol de la cadena en un pincel decorativo.
        /// </summary>
        /// <param name="value">Nombre del rol (Usuario/Asistente).</param>
        /// <param name="targetType">Tipo de destino esperado.</param>
        /// <param name="parameter">Parámetro opcional.</param>
        /// <param name="culture">Cultura.</param>
        /// <returns>Un <see cref="IBrush"/> con la opacidad configurada para el rol; de lo contrario, transparente.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is string role
                ? role.ToLowerInvariant() switch
                {
                    "usuario" => UserBrush,
                    "asistente" => AssistantBrush,
                    _ => Brushes.Transparent,
                }
                : (object)Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}