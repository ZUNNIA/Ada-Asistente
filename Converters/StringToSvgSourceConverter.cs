using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Svg.Skia;
using Serilog;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Proporciona un convertidor de tipos para convertir objetos de cadena de texto (URIs) 
    /// en instancias de <see cref="SvgSource"/> compatibles con el motor de renderizado Skia.
    /// </summary>
    /// <remarks>
    /// Esta clase permite que el motor de XAML de Avalonia realice conversiones automáticas
    /// sin necesidad de declarar convertidores estáticos en cada binding.
    /// </remarks>
    public class StringToSvgSourceConverter : TypeConverter
    {
        /// <summary>
        /// Determina si este convertidor puede convertir un objeto del tipo de origen dado al tipo de este convertidor.
        /// </summary>
        /// <param name="context">Un <see cref="ITypeDescriptorContext"/> que proporciona un contexto de formato.</param>
        /// <param name="sourceType">Un <see cref="Type"/> que representa el tipo desde el que se desea convertir.</param>
        /// <returns>True si el tipo de origen es una cadena (<see cref="string"/>); de lo contrario, false.</returns>
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        /// <summary>
        /// Convierte el objeto dado al tipo de este convertidor, utilizando el contexto y la información de referencia cultural especificados.
        /// </summary>
        /// <param name="context">Un <see cref="ITypeDescriptorContext"/> que proporciona un contexto de formato.</param>
        /// <param name="culture">La <see cref="CultureInfo"/> que se va a usar como cultura actual.</param>
        /// <param name="value">El <see cref="object"/> que se va a convertir (se espera una ruta URI de recurso).</param>
        /// <returns>Una instancia de <see cref="SvgSource"/> cargada con el recurso; de lo contrario, null.</returns>
        /// <exception cref="NotSupportedException">Se lanza si el valor no es una cadena válida o el recurso no existe.</exception>
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string uriString && !string.IsNullOrWhiteSpace(uriString))
            {
                try
                {
                    // El método Load de SvgSource resuelve internamente URIs de tipo 'avares://'
                    return SvgSource.Load(uriString, null);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[StringToSvgSourceConverter] Error crítico cargando SVG: {Uri}", uriString);
                    return null;
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}