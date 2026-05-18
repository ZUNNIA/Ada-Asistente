using AsistenteVirtual.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services.Interfaces
{
    /// <summary>
    /// Contrato para la persistencia y gestión de hilos de conversación en el backend.
    /// </summary>
    public interface IConversationService
    {
        /// <summary>
        /// Recupera el historial resumido de conversaciones del usuario.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión para autorización.</param>
        /// <returns>Tarea con la lista de conversaciones ordenadas por fecha de creación.</returns>
        Task<List<Conversation>> GetConversationsAsync(string userToken);

        /// <summary>
        /// Obtiene la secuencia completa de mensajes de una conversación específica.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="conversationId">Identificador único del hilo en Firestore.</param>
        /// <returns>Tarea con la colección cronológica de mensajes.</returns>
        Task<List<ChatMessage>> GetMessagesAsync(string userToken, string conversationId);

        /// <summary>
        /// Persiste o actualiza los metadatos globales de una conversación.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="conversation">Instancia de la conversación con los datos actualizados.</param>
        /// <returns>Tarea con la conversación procesada por el servidor (incluye IDs generados).</returns>
        Task<Conversation> SaveConversationMetadataAsync(string userToken, Conversation conversation);

        /// <summary>
        /// Guarda un mensaje individual de forma permanente en la base de datos.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="conversationId">ID de la conversación donde se inserta el mensaje.</param>
        /// <param name="message">El objeto de mensaje a persistir.</param>
        /// <returns>Tarea que representa la operación de guardado.</returns>
        Task SaveMessageAsync(string userToken, string conversationId, ChatMessage message);

        /// <summary>
        /// Inicia una conexión de flujo para recibir respuestas de la IA en tiempo real.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="userInput">Texto enviado por el usuario.</param>
        /// <param name="mode">Nombre funcional del modo (ej. "ReasoningMode").</param>
        /// <param name="underlyingMode">Identificador técnico del modelo de IA.</param>
        /// <param name="history">Historial de mensajes previos para dar contexto al modelo.</param>
        /// <param name="files">Lista de archivos adjuntos para análisis multimodal.</param>
        /// <param name="conversationId">ID de la conversación activa.</param>
        /// <returns>Un flujo asíncrono (<see cref="IAsyncEnumerable{T}"/>) de fragmentos de respuesta.</returns>
        IAsyncEnumerable<StreamedBackendResponse> StreamMessageToBackendAsync(string userToken, string userInput, string mode, string underlyingMode, List<ChatMessage> history, List<AttachedFile> files, string conversationId);

        /// <summary>
        /// Elimina físicamente una conversación y todos sus recursos dependientes.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="conversationId">ID único del hilo a eliminar.</param>
        /// <returns>Tarea que representa la operación de borrado.</returns>
        Task DeleteConversationAsync(string userToken, string conversationId);

        /// <summary>
        /// Inicializa una conversación vacía para permitir la subida de archivos previa al chat.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <returns>Tarea con la nueva <see cref="Conversation"/> inicializada.</returns>
        Task<Conversation> PrewarmConversationAsync(string userToken);
    }

    /// <summary>
    /// Contrato para la interacción directa con el almacenamiento de objetos en la nube (GCS).
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Sube un archivo binario a la nube y devuelve su URI interna.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="fileName">Nombre original del archivo.</param>
        /// <param name="fileBytes">Contenido binario del archivo.</param>
        /// <param name="mimeType">Tipo MIME (ej. "application/pdf") para metadatos de almacenamiento.</param>
        /// <param name="conversationId">ID de la conversación asociada.</param>
        /// <returns>Tarea con la URI interna (gs://...) del archivo subido.</returns>
        Task<string> UploadFileAsync(string userToken, string fileName, byte[] fileBytes, string mimeType, string conversationId);

        /// <summary>
        /// Elimina físicamente una lista de archivos de la nube.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="fileIds">Lista de URIs de GCS a eliminar.</param>
        /// <returns>Tarea que representa la operación de eliminación.</returns>
        Task DeleteFilesAsync(string userToken, List<string> fileIds);
    }

    /// <summary>
    /// Contrato para la gestión de datos de perfil, saldos y memorias personalizadas (RAG).
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Obtiene el documento raíz del usuario (perfil y balances de carteras).
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <returns>Tarea con el documento <see cref="UserConversationsDocument"/> o null.</returns>
        Task<UserConversationsDocument?> GetUserDocumentAsync(string userToken);

        /// <summary>
        /// Recupera todas las memorias personalizadas almacenadas por el usuario.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <returns>Tarea con la lista de objetos <see cref="Memory"/>.</returns>
        Task<List<Memory>> GetMemoriesAsync(string userToken);

        /// <summary>
        /// Crea una nueva memoria persistente para el usuario.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="content">Contenido textual de la memoria.</param>
        /// <returns>Tarea con la instancia de <see cref="Memory"/> creada.</returns>
        Task<Memory> AddMemoryAsync(string userToken, string content);

        /// <summary>
        /// Actualiza el contenido de una memoria persistente existente.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="memoryId">ID único de la memoria a modificar.</param>
        /// <param name="content">Nuevo contenido textual.</param>
        /// <returns>Tarea que representa la operación de actualización.</returns>
        Task UpdateMemoryAsync(string userToken, string memoryId, string content);

        /// <summary>
        /// Elimina permanentemente una memoria del sistema.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="memoryId">ID único de la memoria a borrar.</param>
        /// <returns>Tarea que representa la operación de borrado.</returns>
        Task DeleteMemoryAsync(string userToken, string memoryId);
    }

    /// <summary>
    /// Contrato para la orquestación de pagos, planes y suscripciones mediante Stripe.
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Crea una sesión de pago y devuelve la URL de redirección a la pasarela.
        /// </summary>
        /// <param name="userToken">Token JWT de sesión.</param>
        /// <param name="productId">Identificador del producto o plan.</param>
        /// <param name="productType">Tipo de producto ("subscription" o "one_time").</param>
        /// <returns>Tarea con la URL de la sesión de Stripe.</returns>
        Task<string> CreateCheckoutSessionAsync(string userToken, string productId, string productType);
    }
}