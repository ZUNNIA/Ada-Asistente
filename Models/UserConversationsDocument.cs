using System;
using AsistenteVirtual.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa la estructura del documento completo de un usuario en Firestore.
    /// Contiene los IDs de los asistentes globales del usuario y su lista de conversaciones.
    /// </summary>
    public class UserConversationsDocument
    {
        /// <summary>
        /// Contiene los datos del perfil del usuario, como su rol y estado de baneo.
        /// Esto no se mapea directamente a un campo, sino que sus propiedades (Tier, IsBanned)
        /// se leen manualmente en el servicio de persistencia.
        /// </summary>
        public UserProfile UserProfile { get; set; } = new UserProfile();
        /// <summary>
        /// Obtiene o establece el ID del asistente principal del usuario.
        /// Este campo se almacena en Firestore como 'main_assistant_id'.
        /// </summary>
        [JsonPropertyName("main_assistant_id")] 
        public string? MainAssistantId { get; set; }

        /// <summary>
        /// Obtiene o establece el ID del asistente de razonamiento del usuario.
        /// Este campo se almacena en Firestore como 'reasoning_assistant_id'.
        /// </summary>
        [JsonPropertyName("reasoning_assistant_id")]
        public string? ReasoningAssistantId { get; set; }

        /// <summary>
        /// Obtiene o establece el ID del asistente rápido del usuario.
        /// </summary>
        [JsonPropertyName("quick_assistant_id")]
        public string? QuickAssistantId { get; set; }

        /// <summary>
        /// Obtiene o establece la lista de conversaciones del usuario.
        /// </summary>
        [JsonPropertyName("conversations_list")]
        public List<ConversationStoreEntry> Conversations { get; set; } = new List<ConversationStoreEntry>();

        /// <summary>
        /// Obtiene o establece la marca de tiempo de la última actualización del documento.
        /// </summary>
        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Contiene las estadísticas de uso del usuario para las diferentes características.
        /// </summary>
        [JsonPropertyName("usage_stats")]
        public UsageStats UsageStats { get; set; } = new UsageStats();
    }
}
