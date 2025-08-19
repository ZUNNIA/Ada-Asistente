using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa la estructura de datos para una conversación tal como se almacena en Firestore.
    /// Esta clase está decorada con atributos de Firestore para un mapeo automático.
    /// Sirve como un Objeto de Transferencia de Datos (DTO) para la persistencia.
    /// </summary>
    public class ConversationStoreEntry
    {
        [JsonPropertyName("ThreadId")]
        public string? ThreadId { get; set; }
        
        [JsonPropertyName("MainConversationAssistantId")]
        public string? MainConversationAssistantId { get; set; }
        
        [JsonPropertyName("ReasoningConversationAssistantId")]
        public string? ReasoningConversationAssistantId { get; set; }
        
        [JsonPropertyName("WebSearchConversationAssistantId")]
        public string? WebSearchConversationAssistantId { get; set; }

        [JsonPropertyName("VectorStoreId")]
        public string? VectorStoreId { get; set; }
        
        [JsonPropertyName("Title")]
        public string? Title { get; set; }
        [JsonPropertyName("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("AssociatedOpenAIFileIds")]
        public List<string>? AssociatedOpenAIFileIds { get; set; }
        
        [JsonPropertyName("State")]
        public string State { get; set; } = "Active";

        /// <summary>
        /// Constructor sin parámetros requerido para la deserialización de Firestore.
        /// </summary>
        public ConversationStoreEntry() { }

        /// <summary>
        /// Convierte un modelo de dominio <see cref="Conversation"/> a un <see cref="ConversationStoreEntry"/>
        /// para su almacenamiento.
        /// </summary>
        /// <param name="conversation">El modelo de conversación a convertir.</param>
        public static ConversationStoreEntry FromConversation(Conversation conversation)
        {
            return new ConversationStoreEntry
            {
                ThreadId = conversation.ThreadId,
                VectorStoreId = conversation.VectorStoreId,
                Title = conversation.Title,
                CreatedAt = conversation.CreatedAt,
                AssociatedOpenAIFileIds = conversation.AssociatedFileIds,
                State = conversation.State.ToString()
            };
        }

        /// <summary>
        /// Convierte este objeto a un modelo de dominio <see cref="Conversation"/>.
        /// </summary>
        public Conversation ToConversation()
        {
            return new Conversation
            {
                ThreadId = this.ThreadId ?? string.Empty,
                VectorStoreId = this.VectorStoreId,
                Title = this.Title ?? "Conversación sin título",
                CreatedAt = this.CreatedAt,
                AssociatedFileIds = this.AssociatedOpenAIFileIds ?? new List<string>(),
                State = Enum.TryParse<ConversationState>(this.State, out var state) ? state : ConversationState.Active
            };
        }
    }
}
