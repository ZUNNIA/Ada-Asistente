using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa un fragmento (chunk) de datos recibido a través de un flujo de eventos (Server-Sent Events) del backend.
    /// Se utiliza para deserializar las respuestas parciales de la IA en tiempo real.
    /// </summary>
    public class StreamedBackendResponse
    {
        /// <summary>
        /// Obtiene o establece el tipo de evento recibido (ej. "chunk", "image_generated", "done", "error").
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// Obtiene o establece el contenido textual del fragmento.
        /// </summary>
        /// <remarks>Solo presente en eventos de tipo "chunk" o errores.</remarks>
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        /// <summary>
        /// Obtiene o establece la URI pública de acceso al archivo (URL firmada de GCS).
        /// </summary>
        /// <remarks>Solo presente en eventos de tipo "image_generated".</remarks>
        [JsonPropertyName("file_uri")]
        public string? FileUri { get; set; }

        /// <summary>
        /// Obtiene o establece la URI interna de Google Cloud Storage (gs://...).
        /// </summary>
        /// <remarks>Útil para referenciar el archivo en futuras peticiones al backend.</remarks>
        [JsonPropertyName("gcs_uri")]
        public string? GcsUri { get; set; }

        /// <summary>
        /// Propiedad calculada que indica si el flujo de streaming ha finalizado correctamente.
        /// </summary>
        /// <returns>True si el tipo de evento es "done"; de lo contrario, false.</returns>
        [JsonIgnore]
        public bool IsDone => Type == "done";
    }
}