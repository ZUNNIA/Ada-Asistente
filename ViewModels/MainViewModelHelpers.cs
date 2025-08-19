using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

namespace AsistenteVirtual.ViewModels
{
    public partial class MainViewModel
    {
        //==================================================================//
        // MÉTODOS AUXILIARES (LA CAJA DE HERRAMIENTAS)                     //
        //==================================================================//

        /// <summary>
        /// Restaura la UI si un envío de mensaje falla, devolviendo el texto y los archivos al usuario.
        /// </summary>
        private void RollbackFailedUserMessage(ChatMessageViewModel optimisticMessage, string originalText, List<AttachedFileViewModel> originalFiles)
        {
            _dispatcher.Post(() =>
            {
                if (ChatMessages.Contains(optimisticMessage))
                    ChatMessages.Remove(optimisticMessage);

                UserInputText = originalText;
                foreach (var file in originalFiles)
                    TemporaryAttachedFiles.Add(file);
            });
        }

        /// <summary>
        /// Determina si el comando para enviar un mensaje puede ejecutarse.
        /// </summary>
        private bool CanSendMessage() => !IsAssistantResponding;

        /// <summary>
        /// Solicita la cancelación de la respuesta en curso del asistente.
        /// </summary>
        private Task StopResponseAsync()
        {
            _responseCts?.Cancel();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Alterna el estado del modo de razonamiento, asegurando que sea exclusivo con el modo rápido.
        /// </summary>
        private void ToggleReasoningMode()
        {
            IsReasoningModeActive = !IsReasoningModeActive;
            if (IsReasoningModeActive) IsQuickModeActive = false;
        }

        /// <summary>
        /// Alterna el estado del modo rápido, asegurando que sea exclusivo con el modo de razonamiento.
        /// </summary>
        private void ToggleQuickMode()
        {
            IsQuickModeActive = !IsQuickModeActive;
            if (IsQuickModeActive) IsReasoningModeActive = false;
        }

        /// <summary>
        /// Determina el nombre programático de la característica que se está utilizando.
        /// Este es el nombre clave que el backend espera recibir.
        /// </summary>
        private string GetCurrentModeFeatureName()
        {
            if (IsWebSearchModeActive) return "WebSearch";
            if (IsQuickModeActive) return "QuickMode";
            if (IsReasoningModeActive) return "ReasoningMode";
            return "MainMode";
        }

        /// <summary>
        /// Muestra una notificación temporal en la UI.
        /// </summary>
        private void ShowNotification(string message, bool isError)
        {
            _dispatcher.Post(() =>
            {
                var notification = new ErrorNotificationViewModel { Message = message, IsCritical = isError };
                ErrorNotifications.Add(notification);
                _ = Task.Delay(5000).ContinueWith(_ => _dispatcher.Post(() => ErrorNotifications.Remove(notification)));
            });
        }

        /// <summary>
        /// Carga de forma asíncrona el contenido de los archivos de historial de
        /// actualizaciones (changelogs) que están incrustados como recursos en el ensamblado.
        /// </summary>
        private async Task LoadChangelogsAsync()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();
                Log.Information("Recursos incrustados encontrados: {ResourceNames}", string.Join(", ", resourceNames));

                // Búsqueda dinámica del recurso de la app
                var appResourceName = resourceNames.FirstOrDefault(name => name.EndsWith("changelog_app.md", StringComparison.OrdinalIgnoreCase));
                if (appResourceName == null)
                {
                    throw new FileNotFoundException("El recurso 'changelog_app.md' no fue encontrado en el ensamblado.");
                }

                using (var stream = assembly.GetManifestResourceStream(appResourceName))
                using (var reader = new StreamReader(stream!))
                {
                    AppChangelog = await reader.ReadToEndAsync();
                }

                // Búsqueda dinámica del recurso del servidor
                var serverResourceName = resourceNames.FirstOrDefault(name => name.EndsWith("changelog_server.md", StringComparison.OrdinalIgnoreCase));
                if (serverResourceName == null)
                {
                    throw new FileNotFoundException("El recurso 'changelog_server.md' no fue encontrado en el ensamblado.");
                }

                using (var stream = assembly.GetManifestResourceStream(serverResourceName))
                using (var reader = new StreamReader(stream!))
                {
                    ServerChangelog = await reader.ReadToEndAsync();
                }

                OnPropertyChanged(nameof(CurrentChangelogContent));
            }
            catch (Exception ex)
            {
                var errorMessage = "No se pudo cargar el historial. Asegúrate de que los archivos .md estén en el proyecto y su 'Build Action' sea 'EmbeddedResource'.";
                AppChangelog = errorMessage;
                ServerChangelog = errorMessage;
                Log.Error(ex, errorMessage);
                OnPropertyChanged(nameof(CurrentChangelogContent));
            }
        }

        /// <summary>
        /// Carga de forma asíncrona la imagen de perfil del usuario desde una URL.
        /// Se encarga de descargar los datos en un hilo secundario y crear el Bitmap
        /// en el hilo de la UI para evitar problemas de renderizado.
        /// </summary>
        private async Task LoadUserProfileBrushAsync(string? url)
        {
            try
            {
                Bitmap? bitmap = null;

                if (string.IsNullOrWhiteSpace(url))
                {
                    // Carga el bitmap del ícono por defecto
                    using var stream = AssetLoader.Open(new Uri(DefaultAppIcon));
                    bitmap = new Bitmap(stream);
                }
                else
                {
                    // Descarga el bitmap del usuario en un hilo secundario
                    var bytes = await s_httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                    if (bytes != null && bytes.Length > 0)
                    {
                        using var ms = new MemoryStream(bytes);
                        bitmap = new Bitmap(ms);
                    }
                }
                // Ppasamos al hilo de la UI antes de crear cualquier objeto de UI (ImageBrush)
                await _dispatcher.InvokeAsync(() =>
                {
                    if (bitmap != null)
                    {
                        // La creación del ImageBrush ocurre en el hilo correcto.
                        UserProfileBrush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                        Log.Information("[ViewModel] ImageBrush de perfil de usuario creado y asignado exitosamente.");
                    }
                    else
                    {
                        // Si todo falló, nos aseguramos de limpiar el brush.
                        UserProfileBrush = null;
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ViewModel] Falló la carga del ImageBrush para la URL: {ImageUrl}", url);
                // En caso de una excepción, limpiamos el brush en el hilo de la UI.
                await _dispatcher.InvokeAsync(() => { UserProfileBrush = null; });
            }
        }
    }
}