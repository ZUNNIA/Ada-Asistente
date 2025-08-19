using AsistenteVirtual.Commands;
using AsistenteVirtual.Models;
using Avalonia.Platform.Storage;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsistenteVirtual.ViewModels
{
    public partial class MainViewModel
    {
        //==================================================================//
        // LÓGICA DE ARCHIVOS (LA LOGÍSTICA)                                //
        //==================================================================//

        /// <summary>
        /// Abre el selector de archivos y gestiona los archivos seleccionados por el usuario.
        /// </summary>
        private async Task AttachFileAsync()
        {
            var results = await _storageService.PickMultipleFilesAsync();
            if (results == null) return;

            int unsupportedFilesCount = 0;
            foreach (var result in results)
            {
                string extension = Path.GetExtension(result.Name)?.ToLowerInvariant() ?? string.Empty;
                bool isVisionFile = _supportedVisionExtensions.Contains(extension);
                bool isSearchFile = _supportedFileSearchExtensions.Contains(extension);

                if (!isVisionFile && !isSearchFile)
                {
                    unsupportedFilesCount++;
                    continue;
                }

                var attachedFile = new AttachedFile
                {
                    FileName = result.Name,
                    FullPath = result.Path.LocalPath,
                    FileTypeExtension = extension.TrimStart('.'),
                    IsImage = isVisionFile
                };
                
                var vm = new AttachedFileViewModel(attachedFile, _authService, _backendService);
                vm.RemoveCommand = new RelayCommand(async _ => await RemoveTemporaryFileAsync(vm));
                TemporaryAttachedFiles.Add(vm);
            }

            if (unsupportedFilesCount > 0)
                _notificationService.ShowTemporaryNotification($"{unsupportedFilesCount} archivo(s) fueron ignorados por no ser compatibles.", false);
        }
        
        /// <summary>
        /// Gestiona los archivos que el usuario ha arrastrado y soltado sobre la ventana.
        /// Es el punto de entrada para la funcionalidad de 'Drag and Drop'.
        /// </summary>
        /// <param name="files">La lista de archivos que el sistema operativo ha entregado.</param>
        public async Task HandleDroppedFilesAsync(IReadOnlyList<IStorageFile> files)
        {
            if (!files.Any()) return;

            int unsupportedFilesCount = 0;
            foreach (var file in files)
            {
                // DIARIO DEL DESARROLLADOR:
                // Esta lógica es un reflejo casi idéntico de 'AttachFileAsync'.
                // Reutiliza el mismo proceso de validación y creación de ViewModels
                // para mantener la consistencia, sin importar si el archivo viene
                // de un diálogo de selección o si es arrastrado a la ventana.

                string extension = Path.GetExtension(file.Name)?.ToLowerInvariant() ?? string.Empty;
                bool isVisionFile = _supportedVisionExtensions.Contains(extension);
                bool isSearchFile = _supportedFileSearchExtensions.Contains(extension);

                if (!isVisionFile && !isSearchFile)
                {
                    unsupportedFilesCount++;
                    continue;
                }

                var attachedFile = new AttachedFile
                {
                    FileName = file.Name,
                    // ADVERTENCIA:
                    // La ruta de un IStorageFile se obtiene a través de 'TryGetLocalPath()'.
                    // Esto es importante porque no todos los 'IStorageFile' representan
                    // necesariamente un archivo físico en el disco local.
                    FullPath = file.Path.LocalPath,
                    FileTypeExtension = extension.TrimStart('.'),
                    IsImage = isVisionFile
                };
                
                var vm = new AttachedFileViewModel(attachedFile, _authService, _backendService);
                vm.RemoveCommand = new RelayCommand(async _ => await RemoveTemporaryFileAsync(vm));
                TemporaryAttachedFiles.Add(vm);
            }

            if (unsupportedFilesCount > 0)
                _notificationService.ShowTemporaryNotification($"{unsupportedFilesCount} archivo(s) fueron ignorados por no ser compatibles.", false);
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Elimina un archivo adjunto persistente de una conversación.
        /// </summary>
        private async Task RemoveAttachedFileAsync(AttachedFileViewModel fileVM)
        {
            if (_currentConversationHistoryViewModel == null || CurrentUser == null) return;

            var conversationModel = _currentConversationHistoryViewModel.GetModel();
            string? vectorStoreId = conversationModel.VectorStoreId;
            string? fileId = fileVM.FileId;

            if (string.IsNullOrEmpty(vectorStoreId) || string.IsNullOrEmpty(fileId)) return;

            string? userToken = await _authService.GetCurrentUserTokenAsync();
            if (string.IsNullOrEmpty(userToken)) throw new System.InvalidOperationException("Token de usuario no disponible.");

            await _backendService.RemoveFileFromVectorStoreAsync(userToken, vectorStoreId, fileId);
            await _dispatcher.InvokeAsync(() => AttachedFiles.Remove(fileVM));
            conversationModel.AssociatedFileIds.Remove(fileId);
            await _backendService.SaveConversationAsync(userToken, conversationModel);
            _notificationService.ShowTemporaryNotification($"Archivo '{fileVM.FileName}' eliminado.", false);
        }

        /// <summary>
        /// Elimina un archivo de la lista temporal antes de ser enviado.
        /// </summary>
        private async Task RemoveTemporaryFileAsync(AttachedFileViewModel vm)
        {
            await _dispatcher.InvokeAsync(() => TemporaryAttachedFiles.Remove(vm));
            if (!string.IsNullOrWhiteSpace(vm.FileId) && CurrentUser != null)
            {
                string? userToken = await _authService.GetCurrentUserTokenAsync();
                if (!string.IsNullOrEmpty(userToken))
                    await _backendService.DeleteFilesAsync(userToken, new List<string> { vm.FileId });
            }
        }

        /// <summary>
        /// Sube una lista de archivos y devuelve sus IDs asignados por el servidor.
        /// </summary>
        private async Task<List<string>> UploadAndGetFileIdsAsync(List<AttachedFileViewModel> files, CancellationToken token)
        {
            var uploadedFileIds = new List<string>();
            if (!files.Any()) return uploadedFileIds;

            foreach (var fileVM in files)
            {
                token.ThrowIfCancellationRequested();
                // EL TRUCAZO:
                // El ViewModel del archivo (AttachedFileViewModel) gestiona su propia subida
                // en segundo plano tan pronto como se crea. Aquí, simplemente se espera a que
                // ese proceso termine y se recoge el ID del archivo ya subido.
                // Esto mantiene la lógica de subida individual fuera del director de orquesta.
                while (fileVM.State == UploadState.Uploading)
                {
                    await Task.Delay(100, token);
                }

                if (fileVM.State == UploadState.Completed && !string.IsNullOrWhiteSpace(fileVM.FileId))
                    uploadedFileIds.Add(fileVM.FileId!);
            }
            return uploadedFileIds;
        }

        /// <summary>
        /// Carga los archivos asociados a una conversación desde el backend.
        /// </summary>
        private async Task LoadFilesForVMAsync(ConversationHistoryViewModel vm)
        {
            var fileIdsToLoad = vm.GetModel().AssociatedFileIds;
            if (fileIdsToLoad == null || !fileIdsToLoad.Any() || CurrentUser == null) return;
            
            string? userToken = await _authService.GetCurrentUserTokenAsync();
            if (string.IsNullOrEmpty(userToken)) throw new System.Exception("Token de usuario no disponible.");

            List<AttachedFile> files = await _backendService.GetFileDetailsAsync(userToken, fileIdsToLoad);
            await _dispatcher.InvokeAsync(() => {
                foreach (var file in files)
                {
                    var fileVM = new AttachedFileViewModel(file, _authService, _backendService)
                    {
                        RemoveCommand = RemoveAttachedFileCommand
                    };
                    AttachedFiles.Add(fileVM);
                }
            });
        }
    }
}
