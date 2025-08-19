using System.Text.Json.Serialization;
namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa un único mensaje dentro de una conversación.
    /// Esta es la entidad de datos pura, sin ninguna lógica de visualización.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Obtiene o establece el identificador del mensaje asignado por la API de OpenAI.
        /// Puede ser nulo para mensajes optimistas que aún no se han enviado.
        /// </summary>
        [JsonPropertyName("id")]
        public string? MessageId { get; set; }

        /// <summary>
        /// Obtiene o establece el rol del autor del mensaje (ej. "Usuario", "Asistente", "Sistema").
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Obtiene o establece el contenido textual del mensaje.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Indica si la generación de este mensaje fue interrumpida.
        /// </summary>
        public bool IsInterrupted { get; set; }
    }
}
