using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa la estructura de la respuesta JSON que se recibe desde la Cloud Function.
    /// Esto permite una deserialización segura y de tipo fuerte.
    /// </summary>
    public class BackendResponse
    {
        /// <summary>
        /// El contenido textual de la respuesta generada por el asistente de IA.
        /// El atributo JsonPropertyName le dice al deserializador cómo se llama el campo en el JSON.
        /// </summary>
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;

        /// <summary>
        /// El ID del hilo de conversación. Puede ser uno nuevo si la conversación acaba de empezar.
        /// </summary>
        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; } = string.Empty;
    }
}
