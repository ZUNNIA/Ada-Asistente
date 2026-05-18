using AsistenteVirtual.Formatters;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa un único mensaje dentro de una conversación.
    /// Esta es la entidad de datos pura, sin ninguna lógica de visualización.
    /// </summary>
    [MessagePackObject(keyAsPropertyName: true)]
    public class ChatMessage
    {
        /// <summary>
        /// Obtiene o establece el identificador único del mensaje asignado por el backend.
        /// Puede ser nulo para mensajes optimistas que aún no se han enviado.
        /// </summary>
        [JsonPropertyName("id")]
        [Key("MessageId")]
        public string? MessageId { get; set; }

        /// <summary>
        /// Obtiene o establece el rol del autor del mensaje (ej. "Usuario", "Asistente", "Sistema").
        /// </summary>
        [JsonPropertyName("role")]
        [Key("role")]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Obtiene o establece el contenido textual del mensaje.
        /// </summary>
        [JsonPropertyName("content")]
        [Key("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Fecha y hora (UTC) en la que se creó el mensaje.
        /// </summary>
        [JsonPropertyName("Timestamp")]
        [Key("Timestamp")]
        [MessagePackFormatter(typeof(StringDateTimeFormatter))]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indica si la generación de este mensaje fue interrumpida por el usuario o por un error.
        /// </summary>
        [IgnoreMember]
        public bool IsInterrupted { get; set; }

        /// <summary>
        /// Obtiene o establece el el contenido del mensaje cuando tiene una imagen generada
        /// </summary>
        [JsonPropertyName("file_uri")]
        [Key("file_uri")]
        public string? FileUri { get; set; }

        [JsonPropertyName("gcs_uri")]
        [Key("gcs_uri")]
        public string? GcsUri { get; set; }

        /// <summary>
        /// Lista de todas las versiones de texto que ha tenido este mensaje.
        /// </summary>
        [Key("versions")]
        public List<string> Versions { get; set; } = [];

        /// <summary>
        /// Índice de la versión actualmente activa.
        /// </summary>
        [Key("current_version_index")]
        public int CurrentVersionIndex { get; set; } = 0;

        [Key("parent_version_index")]
        [JsonPropertyName("parent_version_index")]
        public int? ParentVersionIndex { get; set; }
    }
}