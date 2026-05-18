using AsistenteVirtual.Formatters;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa la entidad de dominio para una conversación.
    /// Contiene metadatos esenciales, la estructura de archivos adjuntos y el estado del ciclo de vida.
    /// Esta clase está preparada para serialización binaria (MessagePack) y textual (JSON).
    /// </summary>
    [MessagePackObject(keyAsPropertyName: true)]
    public class Conversation
    {
        /// <summary>
        /// Obtiene o establece el identificador único universal (UUID) de la conversación asignado por Firestore.
        /// </summary>
        /// <value>Un string que identifica de forma única la conversación en el backend.</value>
        [Key("ConversationId")]
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>
        /// Obtiene o establece el título descriptivo de la conversación.
        /// </summary>
        /// <value>El nombre que aparece en el historial lateral. Por defecto: "Nueva Conversación".</value>
        [Key("Title")]
        public string Title { get; set; } = "Nueva Conversación";

        /// <summary>
        /// Obtiene o establece la fecha y hora de creación de la conversación en formato UTC.
        /// </summary>
        /// <remarks>
        /// Utiliza <see cref="StringDateTimeFormatter"/> para asegurar compatibilidad entre tipos de fecha de Python y C#.
        /// </remarks>
        [Key("CreatedAt")]
        [MessagePackFormatter(typeof(StringDateTimeFormatter))]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Obtiene o establece la colección de archivos que han sido asociados permanentemente a este hilo de conversación.
        /// </summary>
        /// <value>Una lista de objetos <see cref="AttachedFile"/> procesados por el backend.</value>
        [Key("AssociatedFiles")]
        public List<AttachedFile> AssociatedFiles { get; set; } = [];

        /// <summary>
        /// Obtiene o establece el estado actual de la conversación dentro de la lógica de negocio.
        /// </summary>
        /// <value>Un valor de <see cref="ConversationState"/> (ej. Active, Prewarmed).</value>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [Key("State")]
        public ConversationState State { get; set; } = ConversationState.Active;

        /// <summary>
        /// Obtiene o establece el índice de la versión activa de la conversación.
        /// </summary>
        /// <remarks>
        /// Rastrea diferentes versiones de la conversación cuando el usuario edita mensajes.
        /// </remarks>
        [Key("current_version_index")]
        [JsonPropertyName("current_version_index")]
        public int CurrentVersionIndex { get; set; } = 0;

        /// <summary>
        /// Obtiene o establece el índice de la versión del mensaje padre al que responde esta conversación.
        /// </summary>
        /// <remarks>
        /// Vincula la conversación con una versión específica de un mensaje editado.
        /// </remarks>
        [Key("parent_version_index")]
        [JsonPropertyName("parent_version_index")]
        public int? ParentVersionIndex { get; set; }
    }
}