using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa la estructura de datos pura de una memoria.
    /// Es la entidad fundamental que viaja entre el backend y el cliente.
    /// </summary>
    public class Memory
    {
        /// <summary>
        /// El identificador único de la memoria, asignado por Firestore.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// El contenido textual de la memoria que el asistente recordará.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}