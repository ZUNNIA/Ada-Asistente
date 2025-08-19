using System.IO;
using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa un archivo adjunto, ya sea local o subido a OpenAI.
    /// Contiene solo los datos del archivo, no su estado de subida ni lógica de UI.
    /// </summary>
    public class AttachedFile
    {
        /// <summary>
        /// Identificador único del archivo asignado por la API de OpenAI después de la subida.
        /// </summary>
        [JsonPropertyName("file_id")]
        public string? FileId { get; set; }

        /// <summary>
        /// Nombre del archivo con su extensión.
        /// </summary>
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Ruta completa al archivo en el sistema de archivos local.
        /// Es nulo o vacío para archivos que solo existen en la nube.
        /// </summary>
        public string? FullPath { get; set; }

        /// <summary>
        /// La extensión del archivo sin el punto (ej. "pdf", "txt").
        /// </summary>
        public string FileTypeExtension { get; set; } = string.Empty;

        /// <summary>
        /// Indica si el archivo es una imagen, basado en su extensión.
        /// </summary>
        public bool IsImage { get; set; }
    }
}
