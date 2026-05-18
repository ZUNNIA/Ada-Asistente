using AsistenteVirtual.Commands;
using AsistenteVirtual.Constants;
using AsistenteVirtual.Models;
using AsistenteVirtual.Services.Interfaces;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// Define los estados posibles durante el ciclo de vida de la transferencia de un archivo al servidor.
    /// </summary>
    public enum UploadState
    {
        /// El archivo está en cola y esperando para iniciar la subida.
        Pending,
        /// La transferencia de datos está activa.
        Uploading,
        /// El archivo se ha guardado exitosamente en la nube y tiene una ID válida.
        Completed,
        /// La subida falló debido a un error de red o de servidor.
        Failed,
        /// El usuario detuvo la subida manualmente.
        Cancelled
    }

    /// <summary>
    /// ViewModel encargado de gestionar la lógica de negocio, el estado de carga y la representación visual de un archivo adjunto.
    /// </summary>
    /// <remarks>
    /// Esta clase encapsula un modelo <see cref="AttachedFile"/> y proporciona propiedades reactivas para la UI de Avalonia.
    /// Maneja de forma asíncrona tanto la generación de miniaturas locales como la descarga de previsualizaciones desde la nube.
    /// </remarks>
    public partial class AttachedFileViewModel : ViewModelBase, IDisposable
    {
        #region Campos Privados

        private readonly AttachedFile _file;
        private readonly IFileStorageService _fileStorageService;
        private readonly IAuthService _authService;
        private readonly string _conversationId;

        private CancellationTokenSource? _uploadCts;
        private UploadState _state = UploadState.Pending;
        private string? _errorMessage;
        private Bitmap? _imagePreviewSource;

        #endregion

        #region Propiedades de Enlace (Bindings)

        /// Obtiene o establece el identificador único del archivo en la nube (GCS URI).
        public string? FileId { get => _file.FileId; set => _file.FileId = value; }

        /// Obtiene el nombre del archivo con su extensión.
        public string FileName => _file.FileName;

        /// Obtiene la ruta absoluta en el sistema de archivos local (si existe).
        public string? FullPath => _file.FullPath;

        /// Indica si el archivo es compatible con procesamiento visual.
        public bool IsImage => _file.IsImage;

        /// Obtiene la ruta del recurso de imagen para el icono según la extensión.
        public string IconPath => FileTypeManager.GetIconPathForExtension(_file.FileTypeExtension);

        /// Comando para solicitar la eliminación de este archivo del contexto actual.
        public ICommand RemoveCommand { get; set; }

        /// <summary>
        /// Obtiene o establece el estado actual de la subida. Notifica a la UI para actualizar indicadores visuales.
        /// </summary>
        public UploadState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        /// <summary>
        /// Almacena el mensaje de error técnico en caso de que la subida falle.
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Obtiene o establece el mapa de bits de la previsualización.
        /// </summary>
        /// <remarks> Se utiliza <see cref="Bitmap"/> de Avalonia para renderizado directo en la UI. </remarks>
        public Bitmap? ImagePreviewSource
        {
            get => _imagePreviewSource;
            set => SetProperty(ref _imagePreviewSource, value);
        }

        #endregion

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="AttachedFileViewModel"/>.
        /// </summary>
        /// <param name="file">Modelo de datos del archivo.</param>
        /// <param name="authService">Servicio para obtener tokens de seguridad.</param>
        /// <param name="fileStorageService">Servicio para interactuar con la API de subida.</param>
        /// <param name="conversationId">Identificador de la conversación a la que pertenece el archivo.</param>
        public AttachedFileViewModel(AttachedFile file, IAuthService authService, IFileStorageService fileStorageService, string conversationId)
        {
            _file = file;
            _authService = authService;
            _fileStorageService = fileStorageService;
            _conversationId = conversationId;

            // Inicialización por defecto del comando (debe ser sobrescrito por el VM padre)
            RemoveCommand = new RelayCommand(async _ => await Task.CompletedTask);

            if (!string.IsNullOrWhiteSpace(file.FileId))
            {
                // El archivo ya existe en el servidor (Carga de historial)
                State = UploadState.Completed;
                if (IsImage) { _ = LoadImagePreviewFromGcsUriAsync(); }
            }
            else if (string.IsNullOrEmpty(file.FullPath))
            {
                // Archivo virtual/marcador (ej. .gitkeep para carpetas vacías)
                State = UploadState.Completed;
            }
            else
            {
                // Archivo nuevo pendiente de subida
                _ = LoadLocalImagePreviewAsync();
                _ = StartUploadAsync();
            }
        }

        /// <summary>
        /// Orquesta el proceso de lectura de disco y envío del archivo al backend.
        /// </summary>
        /// <returns>Una tarea que representa la operación de subida.</returns>
        /// <remarks>
        /// El método maneja la lectura de bytes mediante un <see cref="FileStream"/> con permisos compartidos 
        /// para evitar bloqueos si el archivo está abierto en otra aplicación.
        /// </remarks>
        private async Task StartUploadAsync()
        {
            if (State != UploadState.Pending || string.IsNullOrEmpty(FullPath) || !File.Exists(FullPath)) { return; }

            State = UploadState.Uploading;
            _uploadCts = new CancellationTokenSource();

            try
            {
                string? userToken = await _authService.GetCurrentUserTokenAsync();
                if (string.IsNullOrEmpty(userToken)) { throw new InvalidOperationException("Sesión de usuario no válida."); }

                byte[] fileBytes;
                using (FileStream fs = new(FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (MemoryStream ms = new())
                {
                    await fs.CopyToAsync(ms, _uploadCts.Token);
                    fileBytes = ms.ToArray();
                }

                string mimeType = FileTypeManager.GetMimeTypeForExtension(_file.FileTypeExtension);
                string remoteUri = await _fileStorageService.UploadFileAsync(userToken, FileName, fileBytes, mimeType, _conversationId);

                if (!string.IsNullOrWhiteSpace(remoteUri))
                {
                    FileId = remoteUri;
                    State = UploadState.Completed;
                    if (IsImage) { await LoadImagePreviewFromGcsUriAsync(); }
                }
            }
            catch (OperationCanceledException)
            {
                State = UploadState.Cancelled;
            }
            catch (Exception ex)
            {
                State = UploadState.Failed;
                ErrorMessage = ex.Message;
                Log.Error(ex, "[FileVM] Error subiendo archivo {Name}", FileName);
            }
        }

        /// <summary>
        /// Carga una miniatura de la imagen desde una URI de Google Cloud Storage.
        /// </summary>
        private async Task LoadImagePreviewFromGcsUriAsync()
        {
            if (!IsImage || string.IsNullOrEmpty(FileId) || FileId.StartsWith("gs://", StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                using System.Net.Http.HttpClient httpClient = new();
                byte[] imageBytes = await httpClient.GetByteArrayAsync(FileId);
                using MemoryStream ms = new(imageBytes);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ImagePreviewSource = new Bitmap(ms);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[FileVM] Error cargando preview remota para {FileId}", FileId);
                ImagePreviewSource = null;
            }
        }

        /// <summary>
        /// Carga una previsualización de la imagen desde el sistema de archivos local.
        /// </summary>
        private async Task LoadLocalImagePreviewAsync()
        {
            if (!IsImage || string.IsNullOrEmpty(FullPath) || !File.Exists(FullPath))
            {
                return;
            }

            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ImagePreviewSource = new Bitmap(FullPath);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[FileVM] Error cargando preview local para {FullPath}", FullPath);
            }
        }

        /// <summary>
        /// Devuelve el modelo de datos puro asociado a este ViewModel.
        /// </summary>
        /// <returns>La instancia de <see cref="AttachedFile"/>.</returns>
        public AttachedFile GetModel()
        {
            return _file;
        }

        /// <summary>
        /// Cancela operaciones en curso y libera los recursos de previsualización de imagen.
        /// </summary>
        public void Dispose()
        {
            _uploadCts?.Cancel();
            _uploadCts?.Dispose();
            ImagePreviewSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}