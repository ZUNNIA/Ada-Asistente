using System;
using System.Collections.Generic;
using System.Linq;

namespace AsistenteVirtual.Constants
{
    /// <summary>
    /// Define las propiedades de un tipo de archivo soportado, incluyendo su MIME Type para transferencias eficientes.
    /// </summary>
    /// <param name="Extension">La extensión del archivo con el punto (ej. ".pdf").</param>
    /// <param name="IconPath">La ruta al ícono SVG.</param>
    /// <param name="IsVision">Indica si es un archivo de imagen para análisis visual.</param>
    /// <param name="IsSearch">Indica si es un archivo soportado para análisis de contenido (RAG).</param>
    /// <param name="MimeType">El tipo MIME estándar (ej. "application/json", "image/png").</param>
    public record FileTypeDefinition(string Extension, string IconPath, bool IsVision, bool IsSearch, string MimeType);

    /// <summary>
    /// Gestiona de forma centralizada la definición, validación y recursos visuales 
    /// de los tipos de archivos soportados por la aplicación.
    /// </summary>
    public static class FileTypeManager
    {
        /// <summary>
        /// Lista maestra de definiciones de archivos. 
        /// Configura qué extensiones se tratan como imágenes (Visión) y cuáles como texto/código (Search/RAG).
        /// </summary>
        private static readonly List<FileTypeDefinition> s_supportedTypes =
        [
            // --- Imágenes ---
            new(".heic", IconPaths.DefaultFile, IsVision: true,  IsSearch: true, "image/heic"),
            new(".heif", IconPaths.DefaultFile, IsVision: true,  IsSearch: true, "image/heif"),
            new(".jpeg", IconPaths.DefaultFile, IsVision: true,  IsSearch: true, "image/jpeg"),
            new(".jpg",  IconPaths.DefaultFile, IsVision: true,  IsSearch: true, "image/jpeg"),
            new(".png",  IconPaths.DefaultFile, IsVision: true,  IsSearch: true, "image/png"),
            new(".webp", IconPaths.DefaultFile, IsVision: true,  IsSearch: true, "image/webp"),

            // --- UI y Markup ---
            new(".axaml", IconPaths.Text, IsVision: false, IsSearch: true, "text/xml"),
            new(".xaml",  IconPaths.Text, IsVision: false, IsSearch: true, "text/xml"),
            new(".xml",   IconPaths.Text, IsVision: false, IsSearch: true, "text/xml"),
            new(".html",  IconPaths.Html, IsVision: false, IsSearch: true, "text/html"),
            new(".css",   IconPaths.Css,  IsVision: false, IsSearch: true, "text/css"),
            new(".razor", IconPaths.Html, IsVision: false, IsSearch: true, "text/html"),
            new(".svg",   IconPaths.Text, IsVision: false, IsSearch: true, "image/svg+xml"),

            // --- Código Fuente y Scripts ---
            new(".cs",    IconPaths.CSharp,     IsVision: false, IsSearch: true, "text/x-csharp"),
            new(".c",     IconPaths.C,          IsVision: false, IsSearch: true, "text/x-c"),
            new(".cpp",   IconPaths.Cpp,        IsVision: false, IsSearch: true, "text/x-c++"),
            new(".h",     IconPaths.Cpp,        IsVision: false, IsSearch: true, "text/x-c++"),
            new(".py",    IconPaths.Python,     IsVision: false, IsSearch: true, "text/x-python"),
            new(".js",    IconPaths.JavaScript, IsVision: false, IsSearch: true, "application/javascript"),
            new(".ts",    IconPaths.TypeScript, IsVision: false, IsSearch: true, "application/x-typescript"),
            new(".jsx",   IconPaths.JavaScript, IsVision: false, IsSearch: true, "text/javascript"),
            new(".tsx",   IconPaths.TypeScript, IsVision: false, IsSearch: true, "application/x-typescript"),
            new(".java",  IconPaths.Java,       IsVision: false, IsSearch: true, "text/x-java-source"),
            new(".kt",    IconPaths.DefaultFile,IsVision: false, IsSearch: true, "text/x-kotlin"),
            new(".php",   IconPaths.Php,        IsVision: false, IsSearch: true, "application/x-httpd-php"),
            new(".go",    IconPaths.Go,         IsVision: false, IsSearch: true, "text/x-go"),
            new(".rb",    IconPaths.Ruby,       IsVision: false, IsSearch: true, "application/x-ruby"),
            new(".rs",    IconPaths.DefaultFile,IsVision: false, IsSearch: true, "text/rust"),
            new(".swift", IconPaths.DefaultFile,IsVision: false, IsSearch: true, "text/x-swift"),
            new(".sql",   IconPaths.DefaultFile,IsVision: false, IsSearch: true, "application/sql"),
            new(".sh",    IconPaths.Shell,      IsVision: false, IsSearch: true, "application/x-sh"),
            new(".bat",   IconPaths.Shell,      IsVision: false, IsSearch: true, "application/x-bat"),
            new(".ps1",   IconPaths.Shell,      IsVision: false, IsSearch: true, "application/x-powershell"),

            // --- Configuración y Datos ---
            new(".json",   IconPaths.Text,      IsVision: false, IsSearch: true, "application/json"),
            new(".yaml",   IconPaths.Text,      IsVision: false, IsSearch: true, "application/x-yaml"),
            new(".yml",    IconPaths.Text,      IsVision: false, IsSearch: true, "application/x-yaml"),
            new(".toml",   IconPaths.Text,      IsVision: false, IsSearch: true, "application/toml"),
            new(".ini",    IconPaths.Text,      IsVision: false, IsSearch: true, "text/plain"),
            new(".env",    IconPaths.Text,      IsVision: false, IsSearch: true, "text/plain"),
            new(".gradle", IconPaths.Text,      IsVision: false, IsSearch: true, "text/plain"),
            new(".csproj", IconPaths.Text,      IsVision: false, IsSearch: true, "text/xml"),
            new(".sln",    IconPaths.Text,      IsVision: false, IsSearch: true, "text/plain"),
            new(".txt",    IconPaths.Text,      IsVision: false, IsSearch: true, "text/plain"),
            new(".md",     IconPaths.Markdown,  IsVision: false, IsSearch: true, "text/markdown"),
            new(".csv",    IconPaths.Text,      IsVision: false, IsSearch: true, "text/csv"),
            new(".rtf",    IconPaths.Doc,       IsVision: false, IsSearch: true, "application/rtf"),
            new(".tex",    IconPaths.LaTeX,     IsVision: false, IsSearch: true, "application/x-tex")
        ];
        /// <summary>
        /// Obtiene un conjunto hashset con las extensiones soportadas para visión (imágenes).
        /// </summary>
        public static readonly HashSet<string> SupportedVisionExtensions = [.. s_supportedTypes
            .Where(t => t.IsVision)
            .Select(t => t.Extension)];

        /// <summary>
        /// Obtiene un conjunto hashset con todas las extensiones que pueden ser procesadas por el backend.
        /// </summary>
        public static readonly HashSet<string> SupportedFileSearchExtensions = [.. s_supportedTypes
            .Where(t => t.IsSearch)
            .Select(t => t.Extension)];


        /// <summary>
        /// Recupera el MIME Type asociado a una extensión.
        /// </summary>
        /// <param name="extension">La extensión del archivo.</param>
        /// <returns>El MIME type (ej. "application/json") o "application/octet-stream" por defecto.</returns>
        public static string GetMimeTypeForExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) { return "application/octet-stream"; }
            string normalizedExt = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
            FileTypeDefinition? definition = s_supportedTypes.FirstOrDefault(t => t.Extension.Equals(normalizedExt, StringComparison.OrdinalIgnoreCase));
            return definition?.MimeType ?? "application/octet-stream";
        }

        /// <summary>
        /// Recupera la ruta del recurso de icono asociado a una extensión específica.
        /// </summary>
        /// <param name="extension">La extensión del archivo (con o sin punto, ej: ".pdf" o "pdf").</param>
        /// <returns>La ruta URI del recurso (ej: "avares://.../icon.svg").</returns>
        public static string GetIconPathForExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) { return IconPaths.DefaultFile; }

            // Normalizar extensión (asegurar punto inicial y minúsculas)
            string normalizedExt = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
            FileTypeDefinition? definition = s_supportedTypes.FirstOrDefault(t => t.Extension.Equals(normalizedExt, StringComparison.OrdinalIgnoreCase));
            return definition?.IconPath ?? IconPaths.DefaultFile;
        }
    }
}