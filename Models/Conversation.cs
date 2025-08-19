using System;
using System.Collections.Generic;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa los metadatos de una conversación.
    /// Esta es la entidad de datos pura para una entrada en el historial de conversaciones.
    /// </summary>
    public class Conversation
    {
        /// <summary>
        /// Identificador único del hilo (Thread) de OpenAI para esta conversación.
        /// </summary>
        public string ThreadId { get; set; } = string.Empty;

        /// <summary>
        /// Identificador del Vector Store de OpenAI asociado a esta conversación, si existe.
        /// </summary>
        public string? VectorStoreId { get; set; }

        /// <summary>
        /// Título de la conversación que se muestra en el historial.
        /// </summary>
        public string Title { get; set; } = "Nueva Conversación";

        /// <summary>
        /// Fecha y hora (UTC) de creación de la conversación.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Lista de IDs de archivos de OpenAI que están asociados permanentemente a esta conversación.
        /// </summary>
        public List<string> AssociatedFileIds { get; set; } = new List<string>();

        /// <summary>
        /// Estado del ciclo de vida de la conversación (ej. Activa, Pre-calentada).
        /// </summary>
        public ConversationState State { get; set; } = ConversationState.Active;
    }
}
