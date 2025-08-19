using AsistenteVirtual.Commands;
using AsistenteVirtual.Models;
using AsistenteVirtual.Services;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Platform;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// Define los posibles estados de la subida de un archivo a la API.
    /// </summary>
    public enum UploadState { Pending, Uploading, Completed, Failed, Cancelled }

    /// <summary>
    /// ViewModel para un archivo adjunto. Gestiona su estado de subida,
    /// su visualización en la UI y las acciones asociadas, como su eliminación.
    /// </summary>
    public partial class AttachedFileViewModel : ViewModelBase
    {
        // --- Campos Privados ---
        private readonly AttachedFile _file;
        private readonly IBackendService _backendService;
        private readonly IAuthService _authService;
        private CancellationTokenSource? _uploadCts;
        private UploadState _state = UploadState.Pending;
        private string? _errorMessage;
        private Bitmap? _imagePreviewSource;

        // --- Propiedades Públicas ---
        public string? FileId { get => _file.FileId; set => _file.FileId = value; }
        public string FileName => _file.FileName;
        public string? FullPath => _file.FullPath;
        public bool IsImage => _file.IsImage;
        public string IconPath => GetIconPathForExtension(_file.FileTypeExtension);
        public ICommand RemoveCommand { get; set; }

        public UploadState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Obtiene o establece la fuente de la imagen para la vista previa en la UI.
        /// </summary>
        public Bitmap? ImagePreviewSource
        {
            get => _imagePreviewSource;
            set => SetProperty(ref _imagePreviewSource, value);
        }

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="AttachedFileViewModel"/>.
        /// </summary>
        public AttachedFileViewModel(AttachedFile file, IAuthService authService, IBackendService backendService)
        {
            _file = file;
            _authService = authService;
            _backendService = backendService;
            RemoveCommand = new RelayCommand(async _ => await Task.CompletedTask);

            if (!string.IsNullOrWhiteSpace(file.FileId))
            {
                State = UploadState.Completed;
            }
            else
            {
                _ = LoadLocalImagePreviewAsync();
                _ = StartUploadAsync();
            }
        }

        private async Task StartUploadAsync()
        {
            if (State != UploadState.Pending || string.IsNullOrEmpty(FullPath) || !File.Exists(FullPath)) return;

            State = UploadState.Uploading;
            _uploadCts = new CancellationTokenSource();
            Log.Information("[AttachVM] Iniciando subida para: {FileName} a través del backend.", FileName);

            try
            {
                // Obtiene el token del usuario para autenticarnos con el backend.
                string? userToken = await _authService.GetCurrentUserTokenAsync();
                if (string.IsNullOrEmpty(userToken))
                {
                    throw new InvalidOperationException("No se pudo obtener el token de usuario para la subida.");
                }

                // Lee el archivo local en un array de bytes.
                var fileBytes = await File.ReadAllBytesAsync(FullPath, _uploadCts.Token);
                
                // Llama al método en el backend para que él suba el archivo a OpenAI.
                string fileId = await _backendService.UploadFileAsync(userToken, FileName, fileBytes, IsImage);

                if (!string.IsNullOrWhiteSpace(fileId))
                {
                    FileId = fileId;
                    State = UploadState.Completed;
                    Log.Information("[AttachVM] Subida completa para: {FileName}. FileID: {FileId}", FileName, FileId);
                }
                else
                {
                    throw new InvalidOperationException("El backend no devolvió un ID de archivo válido.");
                }
            }
            catch (OperationCanceledException)
            {
                State = UploadState.Cancelled;
                Log.Warning("[AttachVM] Subida cancelada para: {FileName}", FileName);
            }
            catch (Exception ex)
            {
                State = UploadState.Failed;
                ErrorMessage = ex.Message;
                Log.Error(ex, "[AttachVM] Falló la subida para: {FileName}", FileName);
            }
        }

        public AttachedFile GetModel() => _file;

        private async Task LoadLocalImagePreviewAsync()
        {
            if (!IsImage || string.IsNullOrEmpty(FullPath) || !File.Exists(FullPath)) return;

            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ImagePreviewSource = new Bitmap(FullPath);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al cargar la vista previa de la imagen local para {FilePath}", FullPath);
            }
        }

        private static string GetIconPathForExtension(string extension) => extension.ToLowerInvariant() switch
        {
            "c" => "avares://Ada/Resources/Icons/c.svg",
            "cpp" => "avares://Ada/Resources/Icons/c++.svg",
            "cs" => "avares://Ada/Resources/Icons/cs.svg",
            "css" => "avares://Ada/Resources/Icons/css.svg",
            "doc" or "docx" => "avares://Ada/Resources/Icons/doc.svg",
            "go" => "avares://Ada/Resources/Icons/go.svg",
            "html" => "avares://Ada/Resources/Icons/html.svg",
            "java" => "avares://Ada/Resources/Icons/java.svg",
            "js" => "avares://Ada/Resources/Icons/js.svg",
            "md" => "avares://Ada/Resources/Icons/md.svg",
            "pdf" => "avares://Ada/Resources/Icons/pdf.svg",
            "php" => "avares://Ada/Resources/Icons/php.svg",
            "pptx" => "avares://Ada/Resources/Icons/pptx.svg",
            "py" => "avares://Ada/Resources/Icons/py.svg",
            "rb" => "avares://Ada/Resources/Icons/rb.svg",
            "sh" => "avares://Ada/Resources/Icons/sh.svg",
            "tex" => "avares://Ada/Resources/Icons/tex.svg",
            "ts" => "avares://Ada/Resources/Icons/ts.svg",
            "txt" => "avares://Ada/Resources/Icons/txt.svg",
            _ => "avares://Ada/Resources/Icons/file.svg",
        };
    }
}
