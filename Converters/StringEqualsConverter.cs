using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Comparador genérico de cadenas para lógica condicional en XAML.
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        /// <summary>
        /// Compara si el valor del binding coincide con el parámetro estático definido en el XAML.
        /// </summary>
        /// <param name="value">Valor dinámico del ViewModel.</param>
        /// <param name="targetType">Tipo de destino.</param>
        /// <param name="parameter">Valor de referencia a comparar (string).</param>
        /// <param name="culture">Cultura.</param>
        /// <returns>True si las cadenas coinciden ignorando mayúsculas; de lo contrario, False.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string? valStr = value?.ToString();
            string? paramStr = parameter?.ToString();

            return string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}