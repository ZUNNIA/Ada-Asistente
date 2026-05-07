using System.Text.Json.Serialization;
using MessagePack;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa un archivo adjunto dentro de una conversación.
    /// Gestiona la relación entre el archivo físico local y su representación en la nube (GCS).
    /// </summary>
    [MessagePackObject]
    public class AttachedFile
    {
        /// <summary>
        /// Obtiene o establece el identificador único en la nube (URI de GCS).
        /// </summary>
        /// <returns>La ruta gs:// del archivo si ya fue subido.</returns>
        [Key("file_id")]
        [JsonPropertyName("file_id")]
        public string? FileId { get; set; }

        /// <summary>
        /// Obtiene o establece el nombre completo del archivo incluyendo su extensión.
        /// </summary>
        [Key("file_name")]
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Obtiene o establece la ruta absoluta en el disco local.
        /// </summary>
        /// <remarks>Solo disponible durante la sesión donde se adjuntó el archivo originalmente.</remarks>
        [Key("full_path")]
        public string? FullPath { get; set; }

        /// <summary>
        /// Obtiene o establece la extensión del archivo (ej. ".pdf").
        /// </summary>
        [Key("file_type_extension")]
        public string FileTypeExtension { get; set; } = string.Empty;

        /// <summary>
        /// Indica si el archivo debe ser tratado como una imagen para procesamiento visual.
        /// </summary>
        [Key("is_image")]
        public bool IsImage { get; set; }
    }
}
