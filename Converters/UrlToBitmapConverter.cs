using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AsistenteVirtual.ViewModels;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AsistenteVirtual.Converters
{
    /// <summary>
    /// Descarga y gestiona la caché de imágenes desde URLs remotas o recursos locales para su visualización en Avalonia.
    /// Utiliza un patrón de notificación asíncrona para no bloquear la UI durante las descargas.
    /// </summary>
    public class UrlToBitmapConverter : IValueConverter
    {
        private static readonly HttpClient s_httpClient = new();
        private static readonly ConcurrentDictionary<string, Task<Bitmap?>> s_imageCache = new();

        /// <summary>
        /// Inicia la carga de una imagen y devuelve un objeto de notificación para el Binding.
        /// </summary>
        /// <param name="value">La URL o ruta de la imagen (string).</param>
        /// <param name="targetType">Tipo de destino esperado.</param>
        /// <param name="parameter">Parámetro opcional.</param>
        /// <param name="culture">Cultura actual.</param>
        /// <returns>Un <see cref="TaskCompletionNotifier{T}"/> que notificará a la UI cuando el <see cref="Bitmap"/> esté listo.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is string url && !string.IsNullOrWhiteSpace(url) ? new TaskCompletionNotifier<Bitmap?>(GetOrFetchImageAsync(url)) : (object?)null;
        }

        /// <summary>
        /// Obtiene una imagen de la caché o inicia una nueva descarga si no existe.
        /// </summary>
        /// <param name="url">URL absoluta o URI de recurso (avares).</param>
        /// <returns>Una tarea que resulta en el <see cref="Bitmap"/> cargado o null en caso de error.</returns>
        public static Task<Bitmap?> GetOrFetchImageAsync(string url)
        {
            return s_imageCache.GetOrAdd(url, FetchImageAsyncInternal);
        }

        /// <summary>
        /// Lógica interna para resolver la URI y obtener el flujo de datos de la imagen.
        /// </summary>
        /// <param name="url">Ruta de origen.</param>
        /// <returns>El mapa de bits procesado.</returns>
        private static async Task<Bitmap?> FetchImageAsyncInternal(string url)
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == "avares")
                {
                    using Stream stream = AssetLoader.Open(uri);
                    return new Bitmap(stream);
                }

                byte[] imageBytes = await s_httpClient.GetByteArrayAsync(url);
                using MemoryStream ms = new(imageBytes);
                return new Bitmap(ms);
            }
            catch (Exception)
            {
                _ = s_imageCache.TryRemove(url, out _);
                return null;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Notificador especializado para manejar el resultado de tareas asíncronas dentro de bindings de XAML.
        /// Permite que la UI se actualice automáticamente cuando la tarea de descarga finaliza.
        /// </summary>
        /// <typeparam name="T">El tipo del resultado de la tarea (usualmente <see cref="Bitmap"/>).</typeparam>
        public class TaskCompletionNotifier<T> : ViewModelBase
        {
            /// <summary>
            /// Inicializa el notificador y comienza a observar la tarea.
            /// </summary>
            /// <param name="task">La tarea asíncrona a observar.</param>
            public TaskCompletionNotifier(Task<T> task)
            {
                Task = task;
                if (!task.IsCompleted)
                {
                    _ = WatchTaskAsync(task);
                }
            }

            private async Task WatchTaskAsync(Task<T> task)
            {
                try { _ = await task; } catch { /* El error se refleja en la propiedad Result como null/default */ }
                OnPropertyChanged(nameof(Result));
            }

            /// <summary>
            /// Obtiene la tarea que se está ejecutando.
            /// </summary>
            public Task<T> Task { get; }

            /// <summary>
            /// Obtiene el resultado de la tarea una vez finalizada. Notifica a la UI mediante <see cref="OnPropertyChanged"/>.
            /// </summary>
            public T? Result => Task.Status == TaskStatus.RanToCompletion ? Task.Result : default;
        }
    }
}