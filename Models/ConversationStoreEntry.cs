using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Objeto de Transferencia de Datos (DTO) optimizado para la persistencia en Google Cloud Firestore.
    /// Actúa como puente entre la base de datos NoSQL y el modelo de dominio <see cref="Conversation"/>.
    /// </summary>
    public class ConversationStoreEntry
    {
        /// <summary>
        /// Identificador del documento en Firestore.
        /// </summary>
        [JsonPropertyName("ConversationId")]
        public string? ConversationId { get; set; }

        /// <summary>
        /// Identificador del almacén vectorial (Vector Store) asociado en el backend para RAG.
        /// </summary>
        [JsonPropertyName("VectorStoreId")]
        public string? VectorStoreId { get; set; }

        /// <summary>
        /// Título de la conversación almacenado en el registro del usuario.
        /// </summary>
        [JsonPropertyName("Title")]
        public string? Title { get; set; }

        /// <summary>
        /// Marca de tiempo de creación.
        /// </summary>
        [JsonPropertyName("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Colección de archivos adjuntos persistidos.
        /// </summary>
        [JsonPropertyName("AssociatedFiles")]
        public List<AttachedFile> AssociatedFiles { get; set; } = [];

        /// <summary>
        /// Estado de la conversación almacenado como cadena de texto para flexibilidad en la DB.
        /// </summary>
        [JsonPropertyName("State")]
        public string State { get; set; } = "Active";

        /// <summary>
        /// Constructor por defecto requerido para procesos de deserialización automática.
        /// </summary>
        public ConversationStoreEntry() { }

        /// <summary>
        /// Factory Method que crea un DTO de almacenamiento a partir de un objeto de dominio.
        /// </summary>
        /// <param name="conversation">La instancia de <see cref="Conversation"/> de la cual extraer los datos.</param>
        /// <returns>Una nueva instancia de <see cref="ConversationStoreEntry"/> lista para ser enviada al backend.</returns>
        /// <exception cref="ArgumentNullException">Se lanza si la conversación es nula.</exception>
        public static ConversationStoreEntry FromConversation(Conversation conversation)
        {
            ArgumentNullException.ThrowIfNull(conversation);

            return new ConversationStoreEntry
            {
                ConversationId = conversation.ConversationId,
                Title = conversation.Title,
                CreatedAt = conversation.CreatedAt,
                AssociatedFiles = conversation.AssociatedFiles,
                State = conversation.State.ToString()
            };
        }

        /// <summary>
        /// Convierte este DTO de almacenamiento de vuelta a un objeto de dominio rico.
        /// </summary>
        /// <returns>Una instancia de <see cref="Conversation"/> con los datos mapeados y estados validados.</returns>
        public Conversation ToConversation()
        {
            return new Conversation
            {
                ConversationId = ConversationId ?? string.Empty,
                Title = Title ?? "Conversación sin título",
                CreatedAt = CreatedAt,
                AssociatedFiles = AssociatedFiles ?? [],
                State = Enum.TryParse(State, out ConversationState state) ? state : ConversationState.Active
            };
        }
    }
}