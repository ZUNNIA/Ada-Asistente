using AsistenteVirtual.Commands;
using AsistenteVirtual.Converters;
using AsistenteVirtual.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// Define los estados de entrega y persistencia de un mensaje en el flujo de chat.
    /// </summary>
    public enum MessageStatus
    {
        /// El mensaje ha sido confirmado y guardado por el backend.
        Sent,
        /// El mensaje está en tránsito o procesándose en la IA.
        Sending,
        /// Ocurrió un error crítico durante el envío.
        Failed
    }

    /// <summary>
    /// ViewModel que orquesta la representación visual y el comportamiento de una burbuja de mensaje.
    /// </summary>
    /// <remarks>
    /// Esta clase extiende el modelo <see cref="ChatMessage"/> para añadir soporte de:
    /// <list type="bullet">
    /// <item>Gestión de versiones (ediciones previas y respuestas ramificadas).</item>
    /// <item>Carga asíncrona de imágenes generadas por IA.</item>
    /// <item>Estados de edición local y comandos de portapapeles.</item>
    /// </list>
    /// </remarks>
    public partial class ChatMessageViewModel : ViewModelBase
    {
        #region Campos Privados

        private readonly ChatMessage _message;
        private MessageStatus _status;
        private bool _isEditing;
        private string _editText = string.Empty;
        private bool _isVisible = true;
        private string? _fileUri;
        private bool _isImageGenerated;
        private Bitmap? _generatedImageBitmap;
        private int _currentVersionIndex;

        #endregion

        #region Propiedades de Enlace (Bindings)

        /// Obtiene el identificador único del mensaje asignado por la base de datos.
        public string? MessageId => _message.MessageId;

        /// Obtiene el rol del emisor (Usuario/Asistente/Sistema).
        public string Role => _message.Role;

        /// Indica si el mensaje fue enviado por el Asistente para aplicar estilos específicos.
        public bool IsAssistantRole => Role.Equals("Asistente", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Obtiene o establece el contenido textual actual del mensaje.
        /// </summary>
        /// <remarks> Al cambiar, sincroniza el valor con el modelo de datos subyacente. </remarks>
        public string Content
        {
            get => _message.Content;
            set
            {
                if (_message.Content != value)
                {
                    _message.Content = value;
                    OnPropertyChanged();
                }
            }
        }

        /// URI interna del archivo en Google Cloud Storage (gs://).
        public string? GcsUri
        {
            get => _message.GcsUri;
            set
            {
                if (_message.GcsUri != value)
                {
                    _message.GcsUri = value;
                    OnPropertyChanged();
                }
            }
        }

        /// Estado actual del ciclo de vida del mensaje (Enviando, Enviado, Fallido).
        public MessageStatus Status { get => _status; set => SetProperty(ref _status, value); }

        /// Colección observable de todos los textos históricos de este mensaje.
        public ObservableCollection<string> Versions { get; } = [];

        /// <summary>
        /// Obtiene o establece el índice de la versión que se muestra actualmente en la burbuja.
        /// </summary>
        /// <remarks>
        /// Al cambiar el índice, se actualiza automáticamente la propiedad <see cref="Content"/> 
        /// y se dispara el evento <see cref="OnVersionChanged"/> para recalcular la visibilidad de respuestas hijas.
        /// </remarks>
        public int CurrentVersionIndex
        {
            get => _currentVersionIndex;
            set
            {
                if (SetProperty(ref _currentVersionIndex, value))
                {
                    if (value >= 0 && value < Versions.Count)
                    {
                        Content = Versions[value];
                        _message.CurrentVersionIndex = value;
                        OnPropertyChanged(nameof(VersionDisplay));
                        OnPropertyChanged(nameof(CanNavigateVersions));
                        OnVersionChanged?.Invoke();
                    }
                }
            }
        }

        /// Texto formateado para el indicador de páginas (ej. "2 / 5").
        public string VersionDisplay => $"{CurrentVersionIndex + 1} / {Versions.Count}";

        /// Indica si existen múltiples versiones para habilitar los controles de navegación.
        public bool CanNavigateVersions => Versions.Count > 1;

        /// Define si la burbuja está en modo edición (mostrando un TextBox).
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

        /// Texto temporal almacenado durante el proceso de edición.
        public string EditText { get => _editText; set => SetProperty(ref _editText, value); }

        /// <summary> 
        /// Controla la visibilidad de este mensaje en el hilo. 
        /// Se utiliza para ocultar ramas de conversación que no pertenecen a la versión seleccionada del padre.
        /// </summary>
        public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }

        /// Índice de la versión del mensaje padre que disparó esta respuesta.
        public int? ParentVersionIndex { get => _message.ParentVersionIndex; set => _message.ParentVersionIndex = value; }

        /// Indica si la generación de este mensaje fue interrumpida.
        public bool IsInterrupted { get => _message.IsInterrupted; set { _message.IsInterrupted = value; OnPropertyChanged(); } }

        /// Define si este mensaje contiene una imagen generada por IA.
        public bool IsImageGenerated { get => _isImageGenerated; set => SetProperty(ref _isImageGenerated, value); }

        /// <summary>
        /// Obtiene o establece la URI pública de la imagen generada.
        /// </summary>
        /// <remarks> Al establecerse, inicia automáticamente la descarga del <see cref="Bitmap"/>. </remarks>
        public string? FileUri
        {
            get => _fileUri;
            set
            {
                if (SetProperty(ref _fileUri, value))
                {
                    if (!string.IsNullOrEmpty(_fileUri))
                    {
                        IsImageGenerated = true;
                        _ = LoadGeneratedImageAsync(_fileUri);
                    }
                }
            }
        }

        /// Recurso de imagen cargado para renderizado en Avalonia.
        public Bitmap? GeneratedImageBitmap { get => _generatedImageBitmap; private set => SetProperty(ref _generatedImageBitmap, value); }

        #endregion

        #region Eventos y Comandos

        /// Evento notificado cuando el usuario cambia la versión activa del mensaje.
        public event Action? OnVersionChanged;

        /// Comando para habilitar el modo de edición de texto.
        public ICommand EditCommand { get; }

        /// Comando para copiar el contenido textual al portapapeles del sistema.
        public ICommand CopyCommand { get; }

        /// Comando para descartar los cambios realizados en el modo edición.
        public ICommand CancelEditCommand { get; }

        /// Comando para navegar a la versión cronológica anterior.
        public ICommand PreviousVersionCommand { get; }

        /// Comando para navegar a la versión cronológica siguiente.
        public ICommand NextVersionCommand { get; }

        /// Comando para guardar la imagen generada en el disco local del usuario.
        public ICommand DownloadImageCommand { get; }

        #endregion

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="ChatMessageViewModel"/>.
        /// </summary>
        /// <param name="message">Modelo de datos del mensaje.</param>
        /// <param name="initialStatus">Estado inicial del mensaje (Enviado por defecto).</param>
        public ChatMessageViewModel(ChatMessage message, MessageStatus initialStatus = MessageStatus.Sent)
        {
            _message = message;
            _status = initialStatus;

            // Inicialización de colecciones
            if (_message.Versions == null || _message.Versions.Count == 0)
            {
                _message.Versions = [!string.IsNullOrEmpty(_message.Content) ? _message.Content : string.Empty];
            }

            foreach (string v in _message.Versions) { Versions.Add(v); }
            _currentVersionIndex = _message.CurrentVersionIndex;

            // Carga de multimedia si existe
            if (!string.IsNullOrEmpty(message.FileUri)) { FileUri = message.FileUri; }

            // Configuración de Comandos
            EditCommand = new RelayCommand(_ => { EditText = Content; IsEditing = true; return Task.CompletedTask; });
            CancelEditCommand = new RelayCommand(_ => { IsEditing = false; return Task.CompletedTask; });
            PreviousVersionCommand = new RelayCommand(_ => { if (CurrentVersionIndex > 0) { CurrentVersionIndex--; } return Task.CompletedTask; });
            NextVersionCommand = new RelayCommand(_ => { if (CurrentVersionIndex < Versions.Count - 1) { CurrentVersionIndex++; } return Task.CompletedTask; });
            DownloadImageCommand = new RelayCommand(async _ => await DownloadImageAsync());

            CopyCommand = new RelayCommand(async _ => await CopyToClipboardAsync());
        }

        #region Métodos Privados de Lógica

        /// <summary>
        /// Descarga la imagen generada desde la URL y la procesa en un Bitmap de Avalonia.
        /// </summary>
        /// <param name="uri">URL firmada de la imagen.</param>
        private async Task LoadGeneratedImageAsync(string uri)
        {
            try
            {
                GeneratedImageBitmap = await UrlToBitmapConverter.GetOrFetchImageAsync(uri);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MessageVM] Error cargando recurso de imagen para el mensaje.");
            }
        }

        /// <summary>
        /// Procesa la copia de seguridad del contenido al portapapeles detectando el tiempo de ejecución de Avalonia.
        /// </summary>
        private async Task CopyToClipboardAsync()
        {
            if (string.IsNullOrEmpty(Content)) { return; }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                IClipboard? clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(Content);
                }
            }
        }

        /// <summary>
        /// Abre un diálogo nativo de guardado de archivos para persistir la imagen generada.
        /// </summary>
        private async Task DownloadImageAsync()
        {
            if (string.IsNullOrEmpty(FileUri)) { return; }

            try
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    TopLevel? topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
                    if (topLevel == null) { return; }

                    IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Guardar Imagen de Ada",
                        DefaultExtension = ".png",
                        SuggestedFileName = $"Ada_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                        FileTypeChoices = [FilePickerFileTypes.ImagePng]
                    });

                    if (file != null)
                    {
                        using HttpClient client = new();
                        byte[] bytes = await client.GetByteArrayAsync(FileUri);
                        using Stream stream = await file.OpenWriteAsync();
                        await stream.WriteAsync(bytes);
                        Log.Information("[MessageVM] Imagen guardada exitosamente en {Path}", file.Path);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MessageVM] Error al exportar imagen al disco local.");
            }
        }

        /// <summary>
        /// Devuelve el modelo de datos puro asociado a este ViewModel.
        /// </summary>
        /// <returns>Instancia de <see cref="ChatMessage"/>.</returns>
        public ChatMessage GetModel()
        {
            return _message;
        }

        #endregion
    }
}