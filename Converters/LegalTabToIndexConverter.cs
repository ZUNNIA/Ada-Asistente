using System;
using System.Globalization;
using AsistenteVirtual.Constants;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Mapea el nombre de la pestaña legal seleccionada al índice numérico requerido por el slider de la interfaz.
    /// </summary>
    public class LegalTabToIndexConverter : IValueConverter
    {
        /// <summary>
        /// Determina el índice de la pestaña basándose en constantes de <see cref="TabNames"/>.
        /// </summary>
        /// <param name="value">El nombre de la pestaña actual (string).</param>
        /// <param name="targetType">Tipo de destino.</param>
        /// <param name="parameter">Parámetro opcional.</param>
        /// <param name="culture">Cultura.</param>
        /// <returns>1 si la pestaña es 'Terms'; 0 para cualquier otra (por defecto 'Privacy').</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value as string)?.Equals(TabNames.LegalTerms, StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}