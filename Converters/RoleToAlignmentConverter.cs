using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte el rol de un mensaje (ej. "Usuario", "Asistente") a un valor de HorizontalAlignment.
    /// Se utiliza para alinear los mensajes del usuario a la derecha y los del asistente a la izquierda.
    /// </summary>
    public class RoleToAlignmentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string role)
            {
                // Si el rol es "Usuario", alinea el mensaje al final (derecha). Para cualquier otro rol, al inicio (izquierda).
                return string.Equals(role, "Usuario", StringComparison.OrdinalIgnoreCase)
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;
            }
            // Valor por defecto si el binding falla o el valor no es un string.
            return HorizontalAlignment.Left;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // La conversión inversa no es necesaria para este caso de uso.
            throw new NotImplementedException();
        }
    }
}