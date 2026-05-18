using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services.Interfaces
{
    /// <summary>
    /// Abstracción para las interacciones con el sistema de archivos del sistema operativo.
    /// </summary>
    /// <remarks>
    /// Permite a los ViewModels solicitar archivos o carpetas sin depender directamente 
    /// de la ventana principal o el SDK de Avalonia.
    /// </remarks>
    public interface IStorageService
    {
        /// <summary>
        /// Abre el selector de archivos del sistema permitiendo selección múltiple.
        /// </summary>
        /// <returns>Lista de archivos seleccionados o null si se canceló.</returns>
        Task<IReadOnlyList<IStorageFile>?> PickMultipleFilesAsync();

        /// <summary>
        /// Abre el selector de carpetas del sistema.
        /// </summary>
        /// <returns>La carpeta seleccionada o null.</returns>
        Task<IStorageFolder?> PickFolderAsync();
    }
}