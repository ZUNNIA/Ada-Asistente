using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services
{
    /// <summary>
    /// Define un contrato para un servicio que abstrae las interacciones con el sistema de archivos,
    /// como abrir diálogos para seleccionar archivos. Esto mantiene el ViewModel desacoplado de la Vista.
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Abre un diálogo del sistema operativo para que el usuario seleccione uno o más archivos.
        /// </summary>
        /// <returns>
        /// Una lista de solo lectura de objetos IStorageFile que representan los archivos seleccionados,
        /// o null si el usuario cancela la operación.
        /// </returns>
        Task<IReadOnlyList<IStorageFile>?> PickMultipleFilesAsync();
    }
}
