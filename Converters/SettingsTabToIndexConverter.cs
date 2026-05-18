using System;
using System.Globalization;
using AsistenteVirtual.Constants;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte la pestaña de configuración activa en un índice para el control de selección visual.
    /// </summary>
    public class SettingsTabToIndexConverter : IValueConverter
    {
        /// <summary>
        /// Evalúa el nombre de la pestaña de configuración.
        /// </summary>
        /// <param name="value">Nombre de la pestaña (string).</param>
        /// <param name="targetType">Tipo de destino.</param>
        /// <param name="parameter">Parámetro opcional.</param>
        /// <param name="culture">Cultura.</param>
        /// <returns>1 si es la pestaña 'About'; de lo contrario, 0.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value as string)?.Equals(TabNames.SettingsAbout, StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}