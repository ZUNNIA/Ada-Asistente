using System;
using System.Globalization;
using AsistenteVirtual.Constants;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Controla la navegación en el historial de cambios entre la App y el Servidor.
    /// </summary>
    public class UpdateHistoryTabToIndexConverter : IValueConverter
    {
        /// <summary>
        /// Mapea la pestaña de historial a un índice.
        /// </summary>
        /// <param name="value">Nombre de la pestaña activa.</param>
        /// <param name="targetType">Tipo esperado.</param>
        /// <param name="parameter">Parámetro opcional.</param>
        /// <param name="culture">Cultura.</param>
        /// <returns>1 si es 'Servidor'; de lo contrario, 0.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value as string)?.Equals(TabNames.UpdateHistoryServer, StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}