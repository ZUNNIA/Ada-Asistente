using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Determina la alineación horizontal de una burbuja de chat basándose en el rol del remitente.
    /// </summary>
    public class RoleToAlignmentConverter : IValueConverter
    {
        /// <summary>
        /// Convierte el rol del mensaje en un valor de alineación de Avalonia.
        /// </summary>
        /// <param name="value">El rol del mensaje (string).</param>
        /// <param name="targetType">El tipo de destino esperado.</param>
        /// <param name="parameter">Parámetro opcional.</param>
        /// <param name="culture">Cultura actual.</param>
        /// <returns>
        /// <see cref="HorizontalAlignment.Right"/> si el rol es "Usuario"; 
        /// de lo contrario, <see cref="HorizontalAlignment.Left"/>.
        /// </returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is string role
                ? role.Equals("Usuario", StringComparison.OrdinalIgnoreCase)
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left
                : HorizontalAlignment.Left;
        }

        /// <summary>
        /// La conversión inversa no está implementada.
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}