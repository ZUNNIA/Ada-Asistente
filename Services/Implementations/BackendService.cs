using AsistenteVirtual.Models;
using AsistenteVirtual.Services.Interfaces;
using MessagePack;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services.Implementations
{
    /// <summary>
    /// Servicio de infraestructura central que actúa como túnel de comunicación entre el cliente C# y el ecosistema de microservicios en Google Cloud Run.
    /// </summary>
    /// <remarks>
    /// Implementa un patrón de Fachada (Facade) para consolidar operaciones de hilos de conversación, persistencia de archivos, 
    /// gestión de perfiles de usuario y pasarelas de pago. Utiliza una instancia única de <see cref="HttpClient"/> optimizada para 
    /// conexiones de larga duración requeridas por los modelos de IA generativa.
    /// </remarks>
    public class BackendService : IConversationService, IFileStorageService, IUserService, IPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        // --- Configuración de Entorno ---
        private const string _environment = "staging";

        private static readonly Dictionary<string, string> _processMessageUrls = new()
        {
            { "staging", "https://process-message-staging-24416219573.us-south1.run.app" },
            { "production", "https://process-message-prodution-24416219573.us-south1.run.app" }
        };
        private static readonly Dictionary<string, string> _uploadFileUrls = new()
        {
            { "staging", "https://upload-file-staging-24416219573.us-south1.run.app" },
            { "production", "https://upload-file-production-24416219573.us-south1.run.app" }
        };
        private static readonly Dictionary<string, string> _paymentServiceUrls = new()
        {
            { "staging", "https://payment-service-staging-PLACEHOLDER.run.app" },
            { "production", "https://payment-service-prod-PLACEHOLDER.run.app" }
        };

        /// <summary>
        /// Configuración global de serialización para MessagePack.
        /// </summary>
        /// <remarks>
        /// Utiliza un resolvedor compuesto que permite manejar Enums como strings y tipos sin atributos explícitos, 
        /// facilitando la interoperabilidad con los microservicios de Python.
        /// </remarks>
        private static readonly MessagePackSerializerOptions s_msgPackOptions = MessagePackSerializerOptions.Standard
            .WithResolver(MessagePack.Resolvers.CompositeResolver.Create(
                MessagePack.Resolvers.DynamicEnumAsStringResolver.Instance,
                MessagePack.Resolvers.StandardResolver.Instance,
                MessagePack.Resolvers.ContractlessStandardResolver.Instance
            ));

        private readonly string _processMessageUrl;
        private readonly string _uploadFileUrl;
        private readonly string _paymentServiceUrl;

        /// <summary>
        /// Inicializa una nueva instancia del servicio de backend con una configuración de red optimizada.
        /// </summary>
        /// <param name="httpClient">Instancia de <see cref="HttpClient"/> inyectada, configurada preferiblemente mediante IHttpClientFactory.</param>
        public BackendService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Incremento del timeout para soportar latencias altas de modelos de razonamiento (Thinking Mode).

            if (_httpClient.Timeout < TimeSpan.FromMinutes(5))
            {
                _httpClient.Timeout = TimeSpan.FromMinutes(10);
            }

            _processMessageUrl = _processMessageUrls[_environment];
            _uploadFileUrl = _uploadFileUrls[_environment];
            _paymentServiceUrl = _paymentServiceUrls[_environment];
        }

        #region IConversationService Implementation

        /// <summary>
        /// Recupera la lista de metadatos de las conversaciones activas del usuario desde Firestore.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión para autorización Bearer.</param>
        /// <returns>Una tarea que resulta en una colección de <see cref="Conversation"/>.</returns>
        /// <exception cref="HttpRequestException">Se lanza si ocurre un error de red o el servidor responde con un código de error.</exception>
        public async Task<List<Conversation>> GetConversationsAsync(string userToken)
        {
            var requestData = new { action = "get_conversations" };
            ConversationsListResponse response = await PostAndDeserializeAsync<ConversationsListResponse>(userToken, requestData, _processMessageUrl);
            return response.Conversations;
        }

        /// <summary>
        /// Obtiene todos los mensajes de una conversación específica ordenados cronológicamente.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="conversationId">ID único del hilo de conversación en el backend.</param>
        /// <returns>Lista de objetos <see cref="ChatMessage"/>.</returns>
        public async Task<List<ChatMessage>> GetMessagesAsync(string userToken, string conversationId)
        {
            var requestData = new { action = "get_messages", conversation_id = conversationId };
            MessagesListResponse response = await PostAndDeserializeAsync<MessagesListResponse>(userToken, requestData, _processMessageUrl);
            return response.Messages;
        }

        /// <summary>
        /// Persiste o actualiza los metadatos globales de una conversación (título, archivos asociados, estado).
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="conversation">Instancia de la conversación con los datos actualizados.</param>
        /// <returns>La instancia de <see cref="Conversation"/> tal como se guardó en el servidor.</returns>
        public async Task<Conversation> SaveConversationMetadataAsync(string userToken, Conversation conversation)
        {
            if (string.IsNullOrWhiteSpace(conversation.ConversationId))
            {
                Log.Error("[BackendService] Intento de guardar metadatos de una conversación sin ID.");
                throw new InvalidOperationException("No se puede guardar una conversación que no tiene un ID asignado.");
            }

            var requestData = new { action = "save_conversation_metadata", conversation };
            return await PostAndDeserializeAsync<Conversation>(userToken, requestData, _processMessageUrl);
        }

        /// <summary>
        /// Guarda un mensaje individual en la subcolección de mensajes de una conversación.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="conversationId">ID de la conversación destino.</param>
        /// <param name="message">El mensaje a persistir.</param>
        public async Task SaveMessageAsync(string userToken, string conversationId, ChatMessage message)
        {
            var requestData = new { action = "save_message", conversation_id = conversationId, message };
            await PostAsync(userToken, requestData, _processMessageUrl);
        }

        /// <summary>
        /// Elimina físicamente una conversación y purga sus recursos asociados en Firestore y Storage.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="conversationId">ID de la conversación a eliminar.</param>
        public async Task DeleteConversationAsync(string userToken, string conversationId)
        {
            var requestData = new { action = "delete_conversation", conversation_id = conversationId };
            await PostAsync(userToken, requestData, _processMessageUrl);
        }

        /// <summary>
        /// Crea una entrada de conversación vacía en el backend para permitir operaciones previas al chat (como subida de archivos).
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <returns>Una nueva <see cref="Conversation"/> inicializada.</returns>
        public async Task<Conversation> PrewarmConversationAsync(string userToken)
        {
            var requestData = new { action = "prewarm_conversation" };
            return await PostAndDeserializeAsync<Conversation>(userToken, requestData, _processMessageUrl);
        }

        /// <summary>
        /// Establece una conexión de flujo (Streaming) con el backend para recibir respuestas de IA en tiempo real.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="userInput">Texto enviado por el usuario.</param>
        /// <param name="mode">Modo de operación (ej. QuickMode).</param>
        /// <param name="underlyingMode">Nombre técnico del modelo de IA.</param>
        /// <param name="history">Historial de mensajes para contexto del modelo.</param>
        /// <param name="files">Archivos adjuntos para análisis multimodal.</param>
        /// <param name="conversationId">ID de la conversación activa.</param>
        /// <returns>Un flujo asíncrono (<see cref="IAsyncEnumerable{T}"/>) de fragmentos de respuesta.</returns>
        /// <remarks>
        /// Esta función utiliza Server-Sent Events (SSE) para procesar la respuesta palabra por palabra, mejorando la percepción de velocidad.
        /// </remarks>
        public async IAsyncEnumerable<StreamedBackendResponse> StreamMessageToBackendAsync(
            string userToken, string userInput, string mode, string underlyingMode,
            List<ChatMessage> history, List<AttachedFile> files, string conversationId)
        {
            var requestData = new
            {
                action = "send_message",
                user_input = userInput,
                mode,
                underlying_mode = underlyingMode,
                history,
                files = (files ?? []).Select(f => new { file_id = f.FileId, file_name = f.FileName }).ToList(),
                conversation_id = conversationId,
            };

            HttpRequestMessage request = CreateMessagePackRequest(userToken, requestData, _processMessageUrl);
            HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            _ = response.EnsureSuccessStatusCode();

            await foreach (StreamedBackendResponse chunk in ReadStreamAsync(response))
            {
                yield return chunk;
            }
        }

        #endregion

        #region IFileStorageService Implementation

        /// <summary>
        /// Orquesta el proceso de subida de archivos obteniendo una URL firmada y realizando un PUT directo a Google Cloud Storage.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="fileName">Nombre del archivo original.</param>
        /// <param name="fileBytes">Contenido binario del archivo.</param>
        /// <param name="mimeType">Tipo MIME para configuración de cabeceras en Storage.</param>
        /// <param name="conversationId">ID de la conversación asociada.</param>
        /// <returns>La URI interna de GCS (gs://...) que identifica el archivo.</returns>
        /// <exception cref="InvalidOperationException">Lanzada si la respuesta del backend para la URL firmada es nula.</exception>
        public async Task<string> UploadFileAsync(string userToken, string fileName, byte[] fileBytes, string mimeType, string conversationId)
        {
            var urlRequestData = new { file_name = fileName, content_type = mimeType };
            HttpRequestMessage generateUrlRequest = CreateMessagePackRequest(userToken, urlRequestData, $"{_uploadFileUrl}/generate-upload-url");


            HttpResponseMessage response = await _httpClient.SendAsync(generateUrlRequest);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Error solicitando URL de subida: {error}");
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            UploadInfo uploadInfo = JsonSerializer.Deserialize<UploadInfo>(jsonResponse, _jsonOptions)

                ?? throw new InvalidOperationException("No se pudo obtener la información de subida del servidor.");

            using (ByteArrayContent content = new(fileBytes))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                using HttpClient uploadClient = new() { Timeout = TimeSpan.FromMinutes(30) };
                using HttpRequestMessage uploadRequest = new(HttpMethod.Put, uploadInfo.UploadUrl) { Content = content };

                HttpResponseMessage uploadResponse = await uploadClient.SendAsync(uploadRequest);
                if (!uploadResponse.IsSuccessStatusCode)
                {
                    throw new HttpRequestException("La carga binaria directa a la nube falló.");
                }
            }

            return uploadInfo.GcsUri;
        }

        /// <summary>
        /// Solicita al backend la eliminación de múltiples recursos de archivo en la nube.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="fileIds">Lista de URIs de GCS a eliminar.</param>
        public async Task DeleteFilesAsync(string userToken, List<string> fileIds)
        {
            if (fileIds == null || fileIds.Count == 0) { return; }
            var requestData = new { action = "delete_files", file_ids = fileIds };
            await PostAsync(userToken, requestData, _processMessageUrl);
        }

        #endregion

        #region IUserService Implementation

        /// <summary>
        /// Recupera el documento raíz del usuario, consolidando perfil, saldos y lista de hilos.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <returns>El documento <see cref="UserConversationsDocument"/> o null si hay errores.</returns>
        public async Task<UserConversationsDocument?> GetUserDocumentAsync(string userToken)
        {
            var requestData = new { action = "get_user_document" };
            return await PostAndDeserializeAsync<UserConversationsDocument>(userToken, requestData, _processMessageUrl);
        }

        /// <summary>
        /// Obtiene todas las memorias (RAG) almacenadas por el usuario.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <returns>Lista de <see cref="Memory"/>.</returns>
        public async Task<List<Memory>> GetMemoriesAsync(string userToken)
        {
            var requestData = new { action = "get_memories" };
            return await PostAndDeserializeAsync<List<Memory>>(userToken, requestData, _processMessageUrl);
        }

        /// <summary>
        /// Crea una nueva memoria persistente para el usuario.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="content">Contenido de la memoria.</param>
        /// <returns>La instancia de <see cref="Memory"/> generada.</returns>
        public async Task<Memory> AddMemoryAsync(string userToken, string content)
        {
            var requestData = new { action = "add_memory", content };
            return await PostAndDeserializeAsync<Memory>(userToken, requestData, _processMessageUrl);
        }

        /// <summary>
        /// Actualiza el contenido de una memoria existente.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="memoryId">ID de la memoria.</param>
        /// <param name="content">Nuevo contenido.</param>
        public async Task UpdateMemoryAsync(string userToken, string memoryId, string content)
        {
            var requestData = new { action = "update_memory", id = memoryId, content };
            await PostAsync(userToken, requestData, _processMessageUrl);
        }

        /// <summary>
        /// Elimina permanentemente una memoria del sistema.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="memoryId">ID de la memoria a borrar.</param>
        public async Task DeleteMemoryAsync(string userToken, string memoryId)
        {
            var requestData = new { action = "delete_memory", id = memoryId };
            await PostAsync(userToken, requestData, _processMessageUrl);
        }

        #endregion

        #region IPaymentService Implementation

        /// <summary>
        /// Crea una sesión de pago en Stripe a través del microservicio de pagos.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="productId">ID del producto o plan.</param>
        /// <param name="productType">Categoría (subscription/one_time).</param>
        /// <returns>La URL de redirección a la pasarela de pago.</returns>
        public async Task<string> CreateCheckoutSessionAsync(string userToken, string productId, string productType)
        {
            var requestData = new { product_id = productId, product_type = productType };
            CheckoutSessionResponse response = await PostAndDeserializeAsync<CheckoutSessionResponse>(userToken, requestData, $"{_paymentServiceUrl}/create-checkout-session");
            return response.CheckoutUrl;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Crea una petición HTTP optimizada utilizando el formato binario MessagePack.
        /// </summary>
        /// <remarks>
        /// MessagePack reduce significativamente el tamaño del payload comparado con JSON, 
        /// lo cual es vital para el rendimiento en aplicaciones de escritorio que manejan grandes volúmenes de texto.
        /// </remarks>
        /// <param name="userToken">Token Bearer.</param>
        /// <param name="data">Objeto a serializar.</param>
        /// <param name="url">Endpoint destino.</param>
        /// <returns>Un <see cref="HttpRequestMessage"/> listo para ser enviado.</returns>
        private static HttpRequestMessage CreateMessagePackRequest(string userToken, object data, string url)
        {
            HttpRequestMessage message = new(HttpMethod.Post, url);
            if (!string.IsNullOrEmpty(userToken))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            }

            byte[] bytes = MessagePackSerializer.Serialize(data, s_msgPackOptions);
            ByteArrayContent content = new(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-msgpack");
            message.Content = content;
            return message;
        }

        /// <summary>
        /// Ejecuta una petición POST y valida el éxito de la operación.
        /// </summary>
        private async Task PostAsync(string userToken, object data, string url)
        {
            using HttpRequestMessage request = CreateMessagePackRequest(userToken, data, url);
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Log.Error("[Backend] Error {Code} en {Url}: {Error}", response.StatusCode, url, error);
                throw new HttpRequestException(error);
            }
        }

        /// <summary>
        /// Ejecuta una petición POST y deserializa la respuesta binaria o JSON.
        /// </summary>
        /// <typeparam name="T">Tipo de destino de la deserialización.</typeparam>
        /// <returns>El objeto deserializado de tipo <typeparamref name="T"/>.</returns>
        private async Task<T> PostAndDeserializeAsync<T>(string userToken, object data, string url)
        {
            using HttpRequestMessage request = CreateMessagePackRequest(userToken, data, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-msgpack"));


            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(error);
            }

            if (response.Content.Headers.ContentType?.MediaType == "application/x-msgpack")
            {
                using Stream stream = await response.Content.ReadAsStreamAsync();
                return await MessagePackSerializer.DeserializeAsync<T>(stream, s_msgPackOptions);
            }

            using (Stream stream = await response.Content.ReadAsStreamAsync())
            {
                return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions)

                    ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
            }
        }

        /// <summary>
        /// Lector de flujo para eventos SSE (Server-Sent Events).
        /// </summary>
        private async IAsyncEnumerable<StreamedBackendResponse> ReadStreamAsync(HttpResponseMessage response)
        {
            using Stream stream = await response.Content.ReadAsStreamAsync();
            using StreamReader reader = new(stream, Encoding.UTF8);

            while (!reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(':')) { continue; }

                if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    string jsonData = line[6..].Trim();
                    if (string.IsNullOrEmpty(jsonData)) { continue; }

                    StreamedBackendResponse? chunk;

                    try
                    {
                        chunk = JsonSerializer.Deserialize<StreamedBackendResponse>(jsonData, _jsonOptions);
                    }
                    catch (JsonException) { continue; }

                    if (chunk != null)
                    {
                        yield return chunk;
                    }
                }
            }
        }

        private sealed class ConversationsListResponse { [JsonPropertyName("Conversations")] public List<Conversation> Conversations { get; set; } = []; }
        private sealed class MessagesListResponse { [JsonPropertyName("Messages")] public List<ChatMessage> Messages { get; set; } = []; }
        private sealed class UploadInfo { [JsonPropertyName("upload_url")] public string UploadUrl { get; set; } = ""; [JsonPropertyName("gcs_uri")] public string GcsUri { get; set; } = ""; }
        private sealed class CheckoutSessionResponse { [JsonPropertyName("checkout_url")] public string CheckoutUrl { get; set; } = ""; }

        #endregion
    }
}