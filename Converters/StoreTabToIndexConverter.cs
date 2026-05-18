using System;
using System.Globalization;
using AsistenteVirtual.Constants;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Gestiona la transición visual entre suscripciones y paquetes de créditos en la tienda.
    /// </summary>
    public class StoreTabToIndexConverter : IValueConverter
    {
        /// <summary>
        /// Convierte la pestaña de la tienda en un índice entero.
        /// </summary>
        /// <param name="value">Nombre de la pestaña seleccionada.</param>
        /// <param name="targetType">Tipo esperado.</param>
        /// <param name="parameter">Parámetro opcional.</param>
        /// <param name="culture">Cultura.</param>
        /// <returns>1 para la pestaña de paquetes; 0 para suscripciones.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value as string)?.Equals(TabNames.StorePackages, StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}