using AsistenteVirtual.Commands;
using AsistenteVirtual.Constants;
using AsistenteVirtual.Models;
using AsistenteVirtual.Services.Interfaces;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// ViewModel centralizado para la gestión de la experiencia de chat interactivo.
    /// </summary>
    /// <remarks>
    /// Esta clase actúa como el orquestador principal de la vista de chat, integrando:
    /// <list type="bullet">
    /// <item>Gestión de mensajes y flujo de streaming de IA.</item>
    /// <item>Sincronización de archivos adjuntos y carpetas (incluyendo recursividad).</item>
    /// <item>Reconocimiento de voz y dictado en tiempo real.</item>
    /// <item>Gestión de estados de conversación (Activa, Pre-calentada y Nueva).</item>
    /// <item>Control de modos de operación (Quick, Reasoning, WebSearch, Image).</item>
    /// </list>
    /// </remarks>
    public class ChatViewModel : ViewModelBase, IDisposable
    {
        #region Servicios y Dependencias

        private readonly IChatService _chatService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageService _storageService;
        private readonly IVoiceRecognitionService _voiceRecognitionService;
        private readonly INotificationService _notificationService;
        private readonly IConversationService _conversationService;
        private readonly IAuthService _authService;
        private readonly Dispatcher _dispatcher;
        private readonly ILogger _logger;

        #endregion

        #region Campos de Estado Privados

        private string _userInputText = string.Empty;
        private bool _isListening;
        private bool _isQuickModeActive;
        private bool _isReasoningModeActive;
        private bool _isWebSearchModeActive;
        private bool _isImageModeActive;
        private ConversationHistoryViewModel? _currentConversation;
        private Conversation? _prewarmedConversation;
        private string? _pendingConversationId;

        #endregion

        #region Definición de Logs (Optimización)

        private static readonly Action<ILogger, Exception?> s_logLoadMsgError =
            LoggerMessage.Define(LogLevel.Error, new EventId(1, "LoadChatError"), "Error cargando mensajes.");

        private static readonly Action<ILogger, Exception?> s_logSendMsgError =
            LoggerMessage.Define(LogLevel.Error, new EventId(2, "SendMsgError"), "Error enviando mensaje.");

        private static readonly Action<ILogger, string, Exception?> s_logCreatingConversation =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(7, "CreatingConversation"), "[ChatVM] Creando nueva conversación en el servidor para el mensaje: {Message}");

        private static readonly Action<ILogger, string, Exception?> s_logFileDeleted =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "FileDeleted"), "[File Uploader] Archivo {FileName} eliminado de GCS.");

        private static readonly Action<ILogger, string, Exception?> s_logMetadataUpdated =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, "MetadataUpdated"), "[File Uploader] Metadatos de conversación actualizados tras eliminar {FileName}.");

        private static readonly Action<ILogger, Exception?> s_logDeleteFileError =
            LoggerMessage.Define(LogLevel.Error, new EventId(6, "DeleteFileError"), "Error borrando archivo de la nube.");

        #endregion

        #region Colecciones Observables

        /// Colección cronológica de mensajes visualizados en el chat.
        public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

        /// Colección de archivos en cola para ser enviados con el próximo mensaje.
        public ObservableCollection<AttachedFileViewModel> TemporaryFiles { get; } = [];

        /// Colección de archivos que ya forman parte de la conversación persistida.
        public ObservableCollection<AttachedFileViewModel> AttachedFiles { get; } = [];

        #endregion

        #region Propiedades de Enlace (Bindings)

        /// Texto actual ingresado en el cuadro de entrada del usuario.
        public string UserInputText { get => _userInputText; set { if (SetProperty(ref _userInputText, value)) { (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged(); } } }

        /// Indica si el motor de reconocimiento de voz está capturando audio.
        public bool IsListening { get => _isListening; set => SetProperty(ref _isListening, value); }

        /// Propiedad calculada para mostrar/ocultar elementos cuando no hay mensajes.
        public bool IsChatEmpty => Messages.Count == 0;

        /// Define si el modo de respuesta rápida (Flash) está activo.
        public bool IsQuickModeActive { get => _isQuickModeActive; set { if (SetProperty(ref _isQuickModeActive, value) && value) { IsReasoningModeActive = false; } } }

        /// Define si el modo de razonamiento profundo (Thinking) está activo.
        public bool IsReasoningModeActive { get => _isReasoningModeActive; set { if (SetProperty(ref _isReasoningModeActive, value) && value) { IsQuickModeActive = false; } } }

        /// Indica si se debe realizar una búsqueda en la web para fundamentar la respuesta.
        public bool IsWebSearchModeActive { get => _isWebSearchModeActive; set => SetProperty(ref _isWebSearchModeActive, value); }

        /// Indica si el modo de generación de imágenes está habilitado.
        public bool IsImageModeActive { get => _isImageModeActive; set { if (SetProperty(ref _isImageModeActive, value)) { OnPropertyChanged(nameof(ImageModeActivate)); } } }

        /// Ruta del icono dinámico para el botón de modo imagen.
        public static string ImageModeActivate => "avares://Ada/Resources/Icons/mode_image.svg";

        /// Determina si el usuario puede interactuar con los controles de entrada.
        public bool IsUserInputEnabled => !_chatService.IsAssistantResponding;

        #endregion

        #region Comandos

        /// Ejecuta el flujo de envío del mensaje actual.
        public ICommand SendMessageCommand { get; }

        /// Confirma la edición de un mensaje previo y solicita una nueva respuesta de IA.
        public ICommand CommitEditCommand { get; }

        /// Abre el selector de archivos para adjuntar elementos individuales.
        public ICommand AttachFileCommand { get; }

        /// Abre el selector de carpetas para adjuntar directorios completos.
        public ICommand AttachFolderCommand { get; }

        /// Elimina un archivo tanto de la UI como del almacenamiento en la nube.
        public ICommand RemoveAttachedFileCommand { get; }

        /// Solicita al servicio de chat que interrumpa la generación actual.
        public ICommand StopResponseCommand { get; }

        /// Activa o desactiva el dictado por voz.
        public ICommand ToggleVoiceInputCommand { get; }

        /// Alterna el estado del modo rápido.
        public ICommand ToggleQuickModeCommand { get; }

        /// Alterna el estado del modo razonador.
        public ICommand ToggleReasoningModeCommand { get; }

        /// Alterna el estado de búsqueda web.
        public ICommand ToggleWebSearchModeCommand { get; }

        /// Alterna el estado de generación de imágenes.
        public ICommand ToggleImageModeCommand { get; }

        #endregion

        #region Eventos y Delegados

        /// Delegado para solicitar el token de sesión al ViewModel padre.
        public Func<Task<string?>>? RequestUserToken { get; set; }

        /// Se dispara cuando una conversación nueva se activa tras el primer mensaje.
        public Action<Conversation>? OnNewConversationCreated { get; set; }

        #endregion

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="ChatViewModel"/> configurando servicios y comandos.
        /// </summary>
        public ChatViewModel(
            IChatService chatService,
            IFileStorageService fileStorageService,
            IStorageService storageService,
            IVoiceRecognitionService voiceRecognitionService,
            INotificationService notificationService,
            IConversationService conversationService,
            IAuthService authService,
            Dispatcher dispatcher,
            ILogger logger)
        {
            _chatService = chatService;
            _fileStorageService = fileStorageService;
            _storageService = storageService;
            _voiceRecognitionService = voiceRecognitionService;
            _notificationService = notificationService;
            _conversationService = conversationService;
            _authService = authService;
            _dispatcher = dispatcher;
            _logger = logger;

            // Suscripción a cambios de estado del asistente
            _chatService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IChatService.IsAssistantResponding))
                {
                    OnPropertyChanged(nameof(IsUserInputEnabled));
                    (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StopResponseCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            };

            // Configuración de eventos de voz
            _voiceRecognitionService.OnPartialResult += text => _dispatcher.InvokeAsync(() => UserInputText = text);
            _voiceRecognitionService.OnFinalResult += text => _dispatcher.InvokeAsync(() => UserInputText = text);

            // Inicialización de comandos con lógica de validación
            SendMessageCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => IsUserInputEnabled);

            CommitEditCommand = new RelayCommand(async obj =>
            {
                if (obj is ChatMessageViewModel msgVm && !string.IsNullOrWhiteSpace(msgVm.EditText))
                {
                    msgVm.IsEditing = false;
                    if (msgVm.Content == msgVm.EditText) { return; }

                    msgVm.GetModel().Versions.Add(msgVm.EditText);
                    msgVm.Versions.Add(msgVm.EditText);
                    msgVm.CurrentVersionIndex = msgVm.Versions.Count - 1;
                    msgVm.Content = msgVm.EditText;

                    int index = Messages.IndexOf(msgVm);
                    while (Messages.Count > index + 1) { Messages.RemoveAt(index + 1); }

                    MessageContext context = new()
                    {
                        UserInput = msgVm.Content,
                        ChatMessages = Messages,
                        CurrentConversation = _currentConversation,
                        IsEditing = true,
                        FeatureName = GetCurrentFeatureName(),
                        UnderlyingMode = GetUnderlyingMode()
                    };
                    await _chatService.SendMessageAsync(context);
                }
            });

            AttachFileCommand = new RelayCommand(async _ => await AttachFileAsync());
            AttachFolderCommand = new RelayCommand(async _ => await AttachFolderAsync());
            RemoveAttachedFileCommand = new RelayCommand(async vm => { if (vm is AttachedFileViewModel f) { await RemoveFileAsync(f); } });
            StopResponseCommand = new RelayCommand(async _ => await _chatService.StopResponseAsync(), _ => !IsUserInputEnabled);
            ToggleVoiceInputCommand = new RelayCommand(_ => { IsListening = !IsListening; if (IsListening) { _voiceRecognitionService.StartListening(); } else { _voiceRecognitionService.StopListening(); } return Task.CompletedTask; });
            ToggleQuickModeCommand = new RelayCommand(_ => { IsQuickModeActive = !IsQuickModeActive; return Task.CompletedTask; });
            ToggleReasoningModeCommand = new RelayCommand(_ => { IsReasoningModeActive = !IsReasoningModeActive; return Task.CompletedTask; });
            ToggleWebSearchModeCommand = new RelayCommand(_ => { IsWebSearchModeActive = !IsWebSearchModeActive; return Task.CompletedTask; });
            ToggleImageModeCommand = new RelayCommand(_ => { IsImageModeActive = !IsImageModeActive; return Task.CompletedTask; });

            Messages.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    RefreshMessageVisibility();
                }
                OnPropertyChanged(nameof(IsChatEmpty));
            };
        }

        #region Lógica de Negocio de Conversación

        /// <summary>
        /// Carga el historial de mensajes y archivos de una conversación seleccionada.
        /// </summary>
        /// <param name="vm">ViewModel de la conversación en el historial.</param>
        /// <param name="userToken">Token de sesión para autorizar la descarga.</param>
        /// <returns>Tarea que representa la finalización de la carga.</returns>
        /// <summary>
        /// Carga el historial de mensajes y archivos de una conversación seleccionada sin bloquear la UI.
        /// </summary>
        public async Task LoadConversationAsync(ConversationHistoryViewModel vm, string userToken)
        {
            if (_pendingConversationId == vm.ConversationId)
            {
                _currentConversation = vm;
                _pendingConversationId = null;
                return;
            }

            _currentConversation = vm;
            _prewarmedConversation = null;
            Messages.Clear();
            AttachedFiles.Clear();
            TemporaryFiles.Clear();

            try
            {
                List<ChatMessage> rawMsgs = await _conversationService.GetMessagesAsync(userToken, vm.ConversationId);
                await _dispatcher.InvokeAsync(() =>
                {
                    foreach (ChatMessage msg in rawMsgs)
                    {
                        // Forzamos el rol correcto si viene de Python (user/model)
                        if (msg.Role == "user") { msg.Role = "Usuario"; }
                        if (msg.Role == "model") { msg.Role = "Asistente"; }

                        ChatMessageViewModel msgVm = new(msg)
                        {
                            // Restaura el índice que viene de la DB
                            CurrentVersionIndex = msg.CurrentVersionIndex
                        };
                        msgVm.OnVersionChanged += RefreshMessageVisibility;
                        Messages.Add(msgVm);
                    }
                    // Ejecutamos la visibilidad después de haber añadido todos
                    RefreshMessageVisibility();
                });
            }
            catch (Exception ex)
            {
                s_logLoadMsgError(_logger, ex);
                _notificationService.ShowTemporaryNotification("Error sincronizando el hilo de mensajes.", true);
            }
        }

        /// <summary>
        /// Limpia el estado actual para iniciar una conversación desde cero.
        /// </summary>
        public void Clear()
        {
            _currentConversation = null;
            _prewarmedConversation = null;
            Messages.Clear();
            AttachedFiles.Clear();
            TemporaryFiles.Clear();
            UserInputText = string.Empty;
        }

        /// <summary>
        /// Recalcula la visibilidad de los mensajes del asistente basándose en la versión activa del mensaje padre.
        /// </summary>
        public void RefreshMessageVisibility()
        {
            // Usamos Post para asegurar que se ejecute después de cualquier cambio en la colección
            _dispatcher.Post(() =>
            {
                for (int i = 0; i < Messages.Count; i++)
                {
                    ChatMessageViewModel current = Messages[i];
                    // Los mensajes de usuario siempre son visibles
                    if (current.Role == "Usuario")
                    {
                        current.IsVisible = true;
                        continue;
                    }

                    // Para los mensajes del asistente, buscamos su "padre" (el mensaje de usuario anterior)
                    // Un asistente siempre responde al mensaje de usuario inmediatamente anterior en la lista lógica
                    ChatMessageViewModel? previousUserMsg = Messages.Take(i).LastOrDefault(m => m.Role == "Usuario");

                    if (previousUserMsg != null)
                    {
                        // El asistente solo es visible si su ParentVersionIndex coincide con la versión 
                        // que el usuario tiene seleccionada en este momento
                        int versionToMatch = current.ParentVersionIndex ?? 0;
                        current.IsVisible = versionToMatch == previousUserMsg.CurrentVersionIndex;
                    }
                }
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Orquesta el envío del mensaje, inyectando contexto de imagen si es necesario.
        /// </summary>
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(UserInputText) && !TemporaryFiles.Any()) { return; }

            string? token = await GetTokenOrNotify();
            if (token == null) { return; }

            List<AttachedFileViewModel> filesToProcess = [.. TemporaryFiles];

            // Inyección automática de contexto para edición de imágenes
            if (IsImageModeActive)
            {
                ChatMessageViewModel? lastImageMessage = Messages.LastOrDefault(m => m.IsImageGenerated && !string.IsNullOrEmpty(m.GcsUri));
                if (lastImageMessage != null && !filesToProcess.Any(f => f.IsImage))
                {
                    AttachedFile contextFile = new() { FileId = lastImageMessage.GcsUri, FileName = "SUBJECT_REFERENCE.png", IsImage = true };
                    filesToProcess.Add(new AttachedFileViewModel(contextFile, _authService, _fileStorageService, _currentConversation?.ConversationId ?? ""));
                }
            }

            string text = UserInputText;
            UserInputText = string.Empty;
            TemporaryFiles.Clear();

            try
            {
                MessageContext context = new()
                {
                    UserInput = text,
                    TemporaryFiles = filesToProcess,
                    ChatMessages = Messages,
                    CurrentConversation = _currentConversation,
                    FeatureName = GetCurrentFeatureName(),
                    UnderlyingMode = GetUnderlyingMode(),
                    EnsureActiveConversationAsync = (msg, files) => EnsureActiveConversationAsync(token, msg, files)
                };
                await _chatService.SendMessageAsync(context);

                foreach (AttachedFileViewModel f in filesToProcess)
                {
                    if (f.FileName != "SUBJECT_REFERENCE.png")
                    {
                        f.RemoveCommand = RemoveAttachedFileCommand;
                        AttachedFiles.Add(f);
                    }
                }
            }
            catch (Exception ex)
            {
                s_logSendMsgError(_logger, ex);
                UserInputText = text;
                foreach (AttachedFileViewModel f in filesToProcess) { TemporaryFiles.Add(f); }
            }
        }

        #endregion

        #region Gestión de Archivos y Carpetas

        /// <summary>
        /// Abre el diálogo nativo para seleccionar archivos y los añade a la cola de subida.
        /// </summary>
        private async Task AttachFileAsync()
        {
            IReadOnlyList<IStorageFile>? files = await _storageService.PickMultipleFilesAsync();
            if (files == null || !files.Any()) { return; }

            string? token = await GetTokenOrNotify();
            if (token == null) { return; }

            string targetId = await GetUploadTargetId(token);
            foreach (IStorageFile f in files)
            {
                _ = ProcessAndAddFile(f, null, targetId, TemporaryFiles, []);
            }
        }

        /// <summary>
        /// Abre el diálogo nativo para seleccionar una carpeta y realiza un escaneo recursivo de su contenido.
        /// </summary>
        private async Task AttachFolderAsync()
        {
            IStorageFolder? folder = await _storageService.PickFolderAsync();
            if (folder == null) { return; }

            List<AttachedFile> validFiles = [];
            List<string> invalidFiles = [];

            try
            {
                _ = await ScanFolderRecursivelyAsync(folder, folder.Name, validFiles, invalidFiles);
            }
            catch (Exception ex)
            {
                _notificationService.ShowTemporaryNotification($"Error leyendo el directorio: {ex.Message}", true);
                return;
            }

            if (invalidFiles.Count > 0)
            {
                _notificationService.ShowComplexNotification(new ErrorNotificationViewModel
                {
                    Message = $"{invalidFiles.Count} archivos no son compatibles con el asistente.",
                    IsCritical = true,
                    DetailsToCopy = string.Join(Environment.NewLine, invalidFiles.Take(50))
                });
                return;
            }

            string? token = await GetTokenOrNotify();
            if (token == null) { return; }

            string targetId = await GetUploadTargetId(token);
            foreach (AttachedFile af in validFiles)
            {
                TemporaryFiles.Add(new AttachedFileViewModel(af, _authService, _fileStorageService, targetId) { RemoveCommand = RemoveAttachedFileCommand });
            }
        }

        /// <summary>
        /// Escanea recursivamente un <see cref="IStorageFolder"/> para extraer archivos válidos.
        /// </summary>
        /// <remarks>
        /// Si una carpeta está vacía, añade un archivo marcador ".gitkeep" para que la IA entienda la estructura del proyecto.
        /// </remarks>
        private static async Task<bool> ScanFolderRecursivelyAsync(IStorageFolder folder, string relativePathBase, List<AttachedFile> resultList, List<string> invalidFiles)
        {
            bool folderHasContent = false;
            relativePathBase = relativePathBase.Replace("\\", "/");

            await foreach (IStorageItem item in folder.GetItemsAsync())
            {
                if (item is IStorageFile file)
                {
                    if (ProcessSingleFile(file, relativePathBase, resultList, invalidFiles)) { folderHasContent = true; }
                }
                else if (item is IStorageFolder subFolder)
                {
                    string subPath = $"{relativePathBase}/{subFolder.Name}";
                    if (await ScanFolderRecursivelyAsync(subFolder, subPath, resultList, invalidFiles)) { folderHasContent = true; }
                }
            }

            if (!folderHasContent)
            {
                resultList.Add(new AttachedFile { FileName = $"{relativePathBase}/.gitkeep", FileTypeExtension = "gitkeep" });
                return true;
            }
            return folderHasContent;
        }

        /// <summary>
        /// Procesa archivos arrastrados a la interfaz mediante Drag-and-Drop.
        /// </summary>
        /// <param name="items">Colección de elementos soltados.</param>
        public async Task HandleDroppedItemsAsync(IEnumerable<IStorageItem> items)
        {
            if (items == null || !items.Any()) { return; }
            string? token = await GetTokenOrNotify();
            if (token == null) { return; }

            string targetId = await GetUploadTargetId(token);
            List<string> invalidFiles = [];

            foreach (IStorageItem item in items)
            {
                if (item is IStorageFolder folder)
                {
                    List<AttachedFile> folderFiles = [];
                    _ = await ScanFolderRecursivelyAsync(folder, folder.Name, folderFiles, invalidFiles);
                    foreach (AttachedFile af in folderFiles)
                    {
                        TemporaryFiles.Add(new AttachedFileViewModel(af, _authService, _fileStorageService, targetId) { RemoveCommand = RemoveAttachedFileCommand });
                    }

                }
                else if (item is IStorageFile file)
                {
                    _ = ProcessAndAddFile(file, null, targetId, TemporaryFiles, invalidFiles);
                }
            }

            if (invalidFiles.Count > 0) { _notificationService.ShowTemporaryNotification($"{invalidFiles.Count} archivos omitidos por formato incompatible.", false); }

        }

        #endregion

        #region Helpers Privados

        /// <summary>
        /// Asegura que exista una conversación en el backend antes de realizar operaciones de subida o envío.
        /// </summary>
        private async Task<Conversation?> EnsureActiveConversationAsync(string token, string message, List<AttachedFileViewModel> files)
        {
            // Si ya hay una activa, devolvemos su modelo
            if (_currentConversation != null) { return _currentConversation.GetModel(); }

            if (_prewarmedConversation == null)
            {
                s_logCreatingConversation(_logger, message, null);
                _prewarmedConversation = await _conversationService.PrewarmConversationAsync(token);
            }

            Conversation model = _prewarmedConversation;
            model.Title = message.Length > 30 ? message[..27] + "..." : message;
            model.State = ConversationState.Active;
            model.AssociatedFiles.Clear();
            model.AssociatedFiles.AddRange(files.Select(f => f.GetModel()));
            model = await _conversationService.SaveConversationMetadataAsync(token, model);

            _pendingConversationId = model.ConversationId;
            OnNewConversationCreated?.Invoke(model);

            return model;
        }

        private async Task<string> GetUploadTargetId(string token)
        {
            if (_currentConversation != null) { return _currentConversation.ConversationId; }
            if (_prewarmedConversation != null) { return _prewarmedConversation.ConversationId; }

            _prewarmedConversation = await _conversationService.PrewarmConversationAsync(token);
            return _prewarmedConversation.ConversationId;
        }

        private string GetCurrentFeatureName()
        {
            return IsImageModeActive ? FeatureNames.ImageMode : IsWebSearchModeActive ? FeatureNames.WebSearch : FeatureNames.MainMode;
        }


        private string GetUnderlyingMode()
        {
            return IsQuickModeActive ? FeatureNames.QuickMode : IsReasoningModeActive ? FeatureNames.SuperAda : FeatureNames.MainMode;
        }


        private async Task<string?> GetTokenOrNotify()
        {
            string? token = await _authService.GetCurrentUserTokenAsync();
            if (token == null)
            {
                _notificationService.ShowTemporaryNotification("Debes iniciar sesión para realizar esta acción.", true);
            }


            return token;
        }

        private static bool ProcessSingleFile(IStorageFile file, string? relativePathFolder, List<AttachedFile> list, List<string> invalidList)
        {
            string ext = Path.GetExtension(file.Name).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !FileTypeManager.SupportedFileSearchExtensions.Contains(ext))
            {
                invalidList.Add(file.Name);
                return false;
            }

            string fileNameForAi = string.IsNullOrEmpty(relativePathFolder) ? file.Name : $"{relativePathFolder}/{file.Name}";
            list.Add(new AttachedFile { FileName = fileNameForAi, FullPath = file.Path.LocalPath, FileTypeExtension = ext, IsImage = FileTypeManager.SupportedVisionExtensions.Contains(ext) });
            return true;
        }

        private bool ProcessAndAddFile(
            IStorageFile file,
            string? relativePath,
            string targetId,
            ObservableCollection<AttachedFileViewModel> collector,
            List<string> invalidList)
        {
            string ext = Path.GetExtension(file.Name).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !FileTypeManager.SupportedFileSearchExtensions.Contains(ext))
            {
                invalidList.Add(file.Name);
                return false;
            }

            string fileName = string.IsNullOrEmpty(relativePath) ? file.Name : $"{relativePath}/{file.Name}";
            AttachedFile af = new()
            {
                FileName = fileName,
                FullPath = file.Path.LocalPath,
                FileTypeExtension = ext,
                IsImage = FileTypeManager.SupportedVisionExtensions.Contains(ext)
            };

            collector.Add(new AttachedFileViewModel(af, _authService, _fileStorageService, targetId)
            {
                RemoveCommand = RemoveAttachedFileCommand
            });
            return true;
        }

        private async Task RemoveFileAsync(AttachedFileViewModel vm)
        {
            string? token = await GetTokenOrNotify();
            if (token == null) { return; }

            _ = AttachedFiles.Remove(vm);
            _ = TemporaryFiles.Remove(vm);

            if (vm.FileId != null)
            {
                try
                {
                    await _fileStorageService.DeleteFilesAsync(token, [vm.FileId]);
                    s_logFileDeleted(_logger, vm.FileName, null);

                    if (_currentConversation != null)
                    {
                        Conversation model = _currentConversation.GetModel();
                        AttachedFile? toRemove = model.AssociatedFiles.FirstOrDefault(f => f.FileId == vm.FileId);
                        if (toRemove != null)
                        {
                            _ = model.AssociatedFiles.Remove(toRemove);
                            _ = await _conversationService.SaveConversationMetadataAsync(token, model);
                            s_logMetadataUpdated(_logger, vm.FileName, null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _notificationService.ShowTemporaryNotification($"Error eliminando recurso: {vm.FileName}", true);
                    s_logDeleteFileError(_logger, ex);
                }
            }
        }

        #endregion

        /// <summary>
        /// Libera recursos y detiene servicios activos.
        /// </summary>
        public void Dispose()
        {
            _voiceRecognitionService.StopListening();
            GC.SuppressFinalize(this);
        }
    }
}