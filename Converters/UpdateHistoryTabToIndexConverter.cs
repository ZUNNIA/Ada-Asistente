using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Convierte el nombre de la pestaña seleccionada en el historial de actualizaciones
    /// a un índice numérico (0 o 1). Se utiliza para controlar la posición del control
    /// deslizante en la interfaz de usuario.
    /// </summary>
    public class UpdateHistoryTabToIndexConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Si la pestaña seleccionada es "Servidor", el índice es 1, si no, es 0 (para "App").
            return value as string == "Servidor" ? 1 : 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}