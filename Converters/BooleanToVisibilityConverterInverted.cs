using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Invierte un valor booleano para controlar la visibilidad o disponibilidad de elementos en la UI.
    /// Útil para ocultar elementos cuando una condición es verdadera.
    /// </summary>
    public class BooleanToVisibilityConverterInverted : IValueConverter
    {
        /// <summary>
        /// Niega el valor lógico de entrada.
        /// </summary>
        /// <param name="value">Valor booleano de origen.</param>
        /// <param name="targetType">Tipo de destino.</param>
        /// <param name="parameter">Parámetro adicional.</param>
        /// <param name="culture">Cultura.</param>
        /// <returns>True si la entrada es False; False si la entrada es True.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !(value is bool boolValue && boolValue);
        }

        /// <summary>
        /// Vuelve a invertir el valor para recuperar el estado original.
        /// </summary>
        /// <returns>El valor booleano invertido.</returns>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !(value is bool boolValue && boolValue);
        }
    }
}