using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte el rol de un mensaje en un Brush de fondo específico.
    /// Permite diferenciar visualmente los mensajes del usuario, del asistente y del sistema.
    /// </summary>
    public class RoleToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string role)
            {
                return role.ToLowerInvariant() switch
                {
                    // Un color de fondo sutil para el usuario.
                    "usuario" => new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)), // #1AFFFFFF
                    // Un fondo ligeramente diferente para el asistente.
                    "asistente" => new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)), // #10FFFFFF
                    // Fondo por defecto para sistema u otros roles.
                    _ => Brushes.Transparent,
                };
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // La conversión inversa no es necesaria.
            throw new NotImplementedException();
        }
    }
}