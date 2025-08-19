using AsistenteVirtual.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services
{
    public interface IBackendService
    {
        /// <summary>
        /// Solicita al backend el historial de mensajes de una conversación específica.
        /// </summary>
        /// <param name="userToken">El token de autenticación del usuario.</param>
        /// <param name="threadId">El ID de la conversación de la cual obtener los mensajes.</param>
        /// <returns>Una lista de objetos ChatMessage.</returns>
        Task<List<ChatMessage>> LoadMessagesAsync(string userToken, string threadId);

        /// <summary>
        /// Envía una solicitud al backend y recibe la respuesta completa de la IA como un stream de texto.
        /// </summary>
        /// <param name="userToken">El token de autenticación del usuario.</param>
        /// <param name="userInput">El texto ingresado por el usuario.</param>
        /// <param name="mode">El modo de operación (ej. "QuickMode").</param>
        /// <param name="threadId">El ID de la conversación actual, si existe.</param>
        /// <param name="fileIds">La lista de IDs de archivos de OpenAI que se han subido.</param>
        /// <returns>Un stream asíncrono que produce los trozos de texto de la respuesta de la IA.</returns>
        IAsyncEnumerable<StreamedBackendResponse> StreamMessageToBackendAsync(string userToken, string userInput, string mode, string? threadId, List<string> fileIds, string? vectorStoreId);
        /// <summary>
        /// Sube un archivo al backend para su procesamiento con OpenAI.
        /// </summary>
        /// <param name="userToken">El token de autenticación del usuario.</param>
        /// <param name="fileName">El nombre del archivo.</param>
        /// <param name="fileBytes">El contenido del archivo como un array de bytes.</param>
        /// <param name="isImage">Indica si el archivo es para visión.</param>
        /// <returns>El ID del archivo asignado por OpenAI.</returns>
        Task<string> UploadFileAsync(string userToken, string fileName, byte[] fileBytes, bool isImage);

        /// <summary>
        /// Envía la solicitud completa del usuario al backend para su procesamiento.
        /// </summary>
        /// <param name="userToken">El token de autenticación de Google del usuario.</param>
        /// <param name="userInput">El texto ingresado por el usuario.</param>
        /// <param name="mode">El modo de operación (ej. "QuickMode").</param>
        /// <param name="threadId">El ID de la conversación actual, si existe.</param>
        /// <param name="fileIds">La lista de IDs de archivos de OpenAI que se han subido.</param>
        /// <returns>Un objeto BackendResponse con la respuesta del asistente y el thread_id.</returns>
        Task<BackendResponse> SendMessageToBackendAsync(string userToken, string userInput, string mode, string? threadId, List<string> fileIds);

        /// <summary>
        /// Solicita al backend que elimine los recursos de OpenAI asociados a una conversación.
        /// Esto incluye el hilo (Thread) y el almacén de vectores (Vector Store).
        /// </summary>
        /// <param name="userToken">El token de autenticación del usuario.</param>
        /// <param name="threadId">El ID del hilo de OpenAI a eliminar.</param>
        /// <param name="vectorStoreId">El ID del almacén de vectores de OpenAI a eliminar (puede ser nulo).</param>
        Task DeleteConversationResourcesAsync(string userToken, string threadId, string? vectorStoreId);

        /// <summary>
        /// Solicita al backend los metadatos de una lista de archivos de OpenAI.
        /// Se utiliza para poblar el panel de archivos con la información correcta.
        /// </summary>
        /// <param name="userToken">El token de autenticación del usuario.</param>
        /// <param name="fileIds">La lista de IDs de archivos de OpenAI de los que se quieren obtener detalles.</param>
        /// <returns>Una lista de objetos AttachedFile con los detalles de cada archivo.</returns>
        Task<List<AttachedFile>> GetFileDetailsAsync(string userToken, List<string> fileIds);

        Task DeleteFilesAsync(string userToken, List<string> fileIds);

        Task RemoveFileFromVectorStoreAsync(string userToken, string vectorStoreId, string fileId);
        Task<UserConversationsDocument?> GetUserDocumentAsync(string userToken);

        /// <summary>
        /// Solicita al backend que guarde o actualice una conversación en Firestore.
        /// </summary>
        /// <param name="userToken">El token de sesión del usuario.</param>
        /// <param name="conversation">El objeto de la conversación a guardar.</param>
        Task SaveConversationAsync(string userToken, Conversation conversation);

        /// <summary>
        /// Solicita al backend que elimine una conversación de Firestore.
        /// </summary>
        /// <param name="userToken">El token de sesión del usuario.</param>
        /// <param name="threadId">El ID de la conversación a eliminar.</param>
        Task DeleteConversationAsync(string userToken, string threadId);
    }
}