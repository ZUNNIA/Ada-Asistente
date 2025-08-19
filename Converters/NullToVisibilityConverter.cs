using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte un objeto nulo o no nulo a un valor booleano para la propiedad IsVisible en Avalonia.
    /// El comportamiento puede invertirse usando un parámetro.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Convierte un objeto a un valor booleano.
        /// </summary>
        /// <param name="value">El objeto del binding.</param>
        /// <param name="parameter">Si el parámetro es la cadena "VisibleIfNull", la lógica se invierte.</param>
        /// <returns>
        /// Por defecto: true si el valor NO es nulo, false si ES nulo.
        /// Con parámetro "VisibleIfNull": true si el valor ES nulo, false si NO es nulo.
        /// </returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool visibleIfNull = string.Equals(parameter as string, "VisibleIfNull", StringComparison.OrdinalIgnoreCase);

            if (visibleIfNull)
            {
                return isNull;
            }

            return !isNull;
        }

        /// <summary>
        /// La conversión inversa no está implementada.
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}