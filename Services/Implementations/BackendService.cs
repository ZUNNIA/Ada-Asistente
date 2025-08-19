using AsistenteVirtual.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services.Implementations
{
    public class BackendService : IBackendService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private const string ProcessMessageUrl = "https://process-message-802177958692.us-south1.run.app";
        private const string UploadFileUrl = "https://upload-file-802177958692.us-south1.run.app";

        /// <summary>
        /// Inicia una conexión de streaming con el backend para recibir la respuesta de la IA.
        /// Este método se conecta al endpoint HTTP, gestiona los errores de conexión inicial
        /// y luego delega la lectura del stream a un método auxiliar para manejar el flujo
        /// de datos de manera asíncrona sin bloquear el hilo principal.
        /// </summary>
        /// <param name="userToken">El token de autenticación de Google del usuario.</param>
        /// <param name="userInput">El texto ingresado por el usuario.</param>
        /// <param name="mode">El modo de operación (ej. "QuickMode").</param>
        /// <param name="threadId">El ID de la conversación actual, si existe.</param>
        /// <param name="fileIds">La lista de IDs de archivos de OpenAI que se han subido.</param>
        /// <returns>Un stream asíncrono (IAsyncEnumerable) de objetos StreamedBackendResponse,
        /// donde cada objeto puede ser un trozo de texto o un evento de finalización.</returns>
        public async IAsyncEnumerable<StreamedBackendResponse> StreamMessageToBackendAsync(string userToken, string userInput, string mode, string? threadId, List<string> fileIds, string? vectorStoreId)
        {
            // 1. Prepara los datos que se envian al backend en formato JSON.
            var requestData = new
            {
                action = "send_message", // Le dice al backend qué operación se quiere realizar
                user_input = userInput,
                mode,
                thread_id = threadId,
                file_ids = fileIds
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 2. Crea la petición HTTP, añadiendo el token de seguridad en la cabecera.
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            HttpResponseMessage response;
            try
            {
                // 3. Envía la petición inicial.
                // 'ResponseHeadersRead' es clave: nos permite empezar a procesar la respuesta
                // tan pronto como lleguen las cabeceras, sin esperar a que se descargue todo el contenido.
                response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

                // Si la respuesta es un código de error (ej. 4xx, 5xx), lanza una excepción inmediatamente.
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                // Si la conexión inicial falla, lo registra y lanza una excepción clara.
                Log.Error(ex, "Fallo la conexión inicial del stream con el backend.");
                throw new Exception($"Error del backend al iniciar el stream: {ex.Message}");
            }

            // 4. Delega la lectura del stream a un método auxiliar.
            // Esto es necesario para cumplir con las reglas de C# que no permiten 'yield return'
            // dentro de un bloque try-catch.
            await foreach (var chunk in ReadStreamAsync(response))
            {
                // 5. Produce cada trozo (chunk) que devuelve el lector del stream.
                // El 'yield return' es lo que convierte a este método en un generador asíncrono.
                yield return chunk;
            }
        }

        /// <summary>
        /// Lee el stream de la respuesta HTTP, que consiste en una serie de objetos JSON.
        /// 1. Parsea cada objeto JSON recibido.
        /// 2. Identifica el 'type' del evento ('chunk', 'done', 'error').
        /// 3. Cede el control con el objeto 'StreamedBackendResponse' apropiado.
        /// 4. Cumple con la regla C# CS1626 al no usar 'yield return' dentro de un try-catch.
        /// </summary>
        private async IAsyncEnumerable<StreamedBackendResponse> ReadStreamAsync(HttpResponseMessage response)
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (!line.StartsWith("data: "))
                {
                    continue;
                }

                string jsonData = line.Substring(6);
                StreamedBackendResponse? responseToSend = null;

                try
                {
                    // Usaa un bloque 'using' para asegurar que el JsonDocument se deseche correctamente.
                    using JsonDocument doc = JsonDocument.Parse(jsonData);
                    JsonElement root = doc.RootElement;

                    string? eventType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

                    // Decidimos qué tipo de objeto crear basado en el 'type' del JSON.
                    // PERO NO se usa 'yield return' aquí. Solo crea el objeto.
                    switch (eventType)
                    {
                        case "chunk":
                            responseToSend = new StreamedBackendResponse
                            {
                                TextChunk = root.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : string.Empty
                            };
                            break;

                        case "done":
                            responseToSend = new StreamedBackendResponse
                            {
                                IsDone = true,
                                ThreadId = root.TryGetProperty("thread_id", out var threadProp) ? threadProp.GetString() : null,
                                VectorStoreId = root.TryGetProperty("vector_store_id", out var vsProp) ? vsProp.GetString() : null,
                                ModelUsed = root.TryGetProperty("model_used", out var modelProp) ? modelProp.GetString() : "desconocido"
                            };
                            break;

                        case "error":
                            string errorMessage = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "Error desconocido" : "Error desconocido";
                            Log.Error("Error recibido desde el stream del backend: {ErrorMessage}", errorMessage);
                            // Para los errores, no envía nada más.
                            yield break;
                    }
                }
                catch (JsonException ex)
                {
                    Log.Error(ex, "Error al parsear el objeto JSON recibido del stream: {Json}", jsonData);
                }
                if (responseToSend != null)
                {
                    yield return responseToSend;

                    // Si el evento era 'done', termina el proceso.
                    if (responseToSend.IsDone)
                    {
                        yield break;
                    }
                }
            }
        }

        // La firma del método incluye los fileIds para enviarlos al backend.
        public async Task<BackendResponse> SendMessageToBackendAsync(string userToken, string userInput, string mode, string? threadId, List<string> fileIds)
        {
            // Crea un objeto anónimo con todos los datos que el backend necesita.
            var requestData = new
            {
                user_input = userInput,
                mode,
                thread_id = threadId,
                file_ids = fileIds
            };

            // Convierte el objeto a un string en formato JSON.
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Crea la petición HTTP, añadiendo el token de Google para la autenticación.
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            Log.Information("Enviando petición al backend: {Url}", ProcessMessageUrl);
            var response = await _httpClient.SendAsync(requestMessage);

            // Lee el contenido de la respuesta del servidor.
            var responseContent = await response.Content.ReadAsStringAsync();

            // Si el servidor devolvió un error (ej. 401 No Autorizado, 403 Límite Superado), lanza una excepción.
            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Error del backend ({StatusCode}): {Response}", response.StatusCode, responseContent);
                // El mensaje de la excepción será el que se muestra al usuario en la notificación.
                throw new Exception(responseContent);
            }

            Log.Information("Respuesta recibida del backend.");
            // Usa el JsonSerializer para convertir el string JSON de la respuesta
            // al objeto C# 'BackendResponse'.
            // Si el JSON no coincide con la estructura de la clase, esto devolverá null.
            var backendResponse = JsonSerializer.Deserialize<BackendResponse>(responseContent);

            if (backendResponse == null)
            {
                // Esto pasaría si el backend cambia su formato de respuesta y la app no está actualizada.
                throw new InvalidOperationException("No se pudo deserializar la respuesta del backend.");
            }

            // Devuelve el objeto completo con todos los datos.
            return backendResponse;
        }
        public async Task<List<ChatMessage>> LoadMessagesAsync(string userToken, string threadId)
        {
            var requestData = new
            {
                action = "load_messages",
                thread_id = threadId
            };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            var response = await _httpClient.SendAsync(requestMessage);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error del backend al cargar mensajes: {responseContent}");
            }

            // Deserializa la respuesta que contiene la lista de mensajes.
            var messagesResponse = JsonSerializer.Deserialize<MessagesListResponse>(responseContent);
            return messagesResponse?.Messages ?? new List<ChatMessage>();
        }

        public async Task<string> UploadFileAsync(string userToken, string fileName, byte[] fileBytes, bool isImage)
        {
            // MultipartFormDataContent es la forma estándar de enviar archivos a través de HTTP.
            using var multipartContent = new MultipartFormDataContent();

            // Añade el archivo como un array de bytes.
            multipartContent.Add(new ByteArrayContent(fileBytes), "file", fileName);
            // Añade otros datos que el backend pueda necesitar.
            multipartContent.Add(new StringContent(isImage.ToString()), "is_image");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, UploadFileUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = multipartContent;

            Log.Information("Enviando archivo '{FileName}' al backend de subida.", fileName);
            var response = await _httpClient.SendAsync(requestMessage);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Error del backend de subida ({StatusCode}): {Response}", response.StatusCode, responseContent);
                throw new Exception($"Error al subir archivo: {responseContent}");
            }

            // Supone que el backend devuelve un JSON con el ID del archivo.
            var jsonDoc = JsonDocument.Parse(responseContent);
            string fileId = jsonDoc.RootElement.GetProperty("file_id").GetString() ?? "";

            return fileId;
        }

        public async Task DeleteConversationResourcesAsync(string userToken, string threadId, string? vectorStoreId)
        {
            // Este método envía la petición de borrado al backend.
            var requestData = new
            {
                action = "delete_resources",
                thread_id = threadId,
                vector_store_id = vectorStoreId
            };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            try
            {
                // No espera la respuesta para que la UI no se bloquee.
                await _httpClient.SendAsync(requestMessage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falló la petición para eliminar recursos de OpenAI.");
            }
        }

        public async Task<List<AttachedFile>> GetFileDetailsAsync(string userToken, List<string> fileIds)
        {
            // Este método pide al backend los detalles de los archivos.
            var requestData = new
            {
                action = "get_file_details",
                file_ids = fileIds
            };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            var response = await _httpClient.SendAsync(requestMessage);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error del backend al obtener detalles de archivos: {responseContent}");
            }

            // Deserializa la respuesta que contiene la lista de archivos.
            var filesResponse = JsonSerializer.Deserialize<FileDetailsListResponse>(responseContent);
            return filesResponse?.Files ?? new List<AttachedFile>();
        }

        public async Task DeleteFilesAsync(string userToken, List<string> fileIds)
        {
            if (fileIds == null || !fileIds.Any()) return;

            var requestData = new
            {
                action = "delete_files",
                file_ids = fileIds
            };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            try
            {
                // Envía la petición pero no espera la respuesta para no bloquear la UI.
                _ = await _httpClient.SendAsync(requestMessage);
                Log.Information("Solicitud de eliminación para {Count} archivo(s) temporales enviada al backend.", fileIds.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falló la petición para eliminar archivos temporales.");
            }
        }

        public async Task RemoveFileFromVectorStoreAsync(string userToken, string vectorStoreId, string fileId)
        {
            var requestData = new
            {
                action = "remove_file_from_vs",
                vector_store_id = vectorStoreId,
                file_id = fileId
            };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                // Lanza una excepción si el backend devolvió un error (ej. 500)
                response.EnsureSuccessStatusCode();
                Log.Information("Solicitud para eliminar el archivo {FileId} del VS {VectorStoreId} completada.", fileId, vectorStoreId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falló la petición para eliminar el archivo adjunto {FileId}.", fileId);
                // Propaga la excepción para que el ViewModel pueda manejarla.
                throw new Exception("No se pudo eliminar el archivo adjunto del servidor.");
            }
        }

        public async Task<UserConversationsDocument?> GetUserDocumentAsync(string userToken)
        {
            // Este método pide al backend el documento completo del usuario.
            var requestData = new { action = "get_user_document" };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            var response = await _httpClient.SendAsync(requestMessage);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error del backend al obtener el documento del usuario: {responseContent}");
            }

            return JsonSerializer.Deserialize<UserConversationsDocument>(responseContent);
        }

        /// <summary>
        /// Envía una conversación al backend para ser guardada en la base de datos.
        /// </summary>
        public async Task SaveConversationAsync(string userToken, Conversation conversation)
        {
            // Prepara los datos para enviar. La acción y el objeto de la conversación.
            var requestData = new
            {
                action = "save_conversation",
                conversation = conversation
            };

            // Serializa los datos a JSON.
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Crea y envía la petición autenticada.
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                // Lanza una excepción si el backend devolvió un error (ej. 500).
                response.EnsureSuccessStatusCode();
                Log.Information("Conversación {ThreadId} guardada exitosamente a través del backend.", conversation.ThreadId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falló la petición para guardar la conversación.");
                // Propaga la excepción para que el ViewModel pueda notificar al usuario.
                throw new Exception("No se pudo guardar la conversación en el servidor.");
            }
        }
        
        /// <summary>
        /// Envía una solicitud al backend para eliminar una conversación.
        /// </summary>
        public async Task DeleteConversationAsync(string userToken, string threadId)
        {
            var requestData = new
            {
                action = "delete_conversation",
                thread_id = threadId
            };
            
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, ProcessMessageUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            requestMessage.Content = content;

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();
                Log.Information("Solicitud para eliminar la conversación {ThreadId} enviada al backend.", threadId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falló la petición para eliminar la conversación {ThreadId}.", threadId);
                throw new Exception("No se pudo eliminar la conversación en el servidor.");
            }
        }
    }
    
    public class MessagesListResponse
    {
        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new();
    }
    
    /// <summary>
    /// Representa la estructura de la respuesta JSON para la petición de detalles de archivos.
    /// Permite una deserialización segura y de tipo fuerte.
    /// </summary>
    public class FileDetailsListResponse
    {
        [JsonPropertyName("files")]
        public List<AttachedFile> Files { get; set; } = new();
    }
}