using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Descarga una imagen desde una URL y la convierte en un Bitmap para Avalonia.
    /// Utiliza un HttpClient estático para la eficiencia.
    /// </summary>
    public class UrlToBitmapConverter : IValueConverter
    {
        private static readonly HttpClient s_httpClient = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            // Se devuelve una tarea que la UI de Avalonia resolverá de forma asíncrona.
            return Task.Run<Bitmap?>(async () =>
            {
                try
                {
                    // Si la URL es un recurso de Avalonia...
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "avares")
                    {
                        // ...se usa el AssetLoader para cargar el recurso.
                        await using var stream = AssetLoader.Open(uri);
                        return new Bitmap(stream);
                    }
                    // Si es una URL web...
                    else if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    {
                        // ...se usa HttpClient para descargarla.
                        var response = await s_httpClient.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        await using var stream = await response.Content.ReadAsStreamAsync();
                        return new Bitmap(stream);
                    }
                }
                catch (Exception)
                {
                    // En caso de cualquier error (ej. 404, sin red, recurso no encontrado),
                    // se devuelve null para que no se muestre ninguna imagen.
                    return null;
                }
                return null;
            });
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
