using AsistenteVirtual.Commands;
using AsistenteVirtual.Constants;
using AsistenteVirtual.Converters;
using AsistenteVirtual.Models;
using AsistenteVirtual.Services.Interfaces;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// ViewModel raíz de la aplicación que actúa como el orquestador central de la interfaz de usuario.
    /// </summary>
    /// <remarks>
    /// Implementa un patrón de "ViewModel de Aplicación" (Main Orchestrator). Gestiona:
    /// <list type="bullet">
    /// <item>El ciclo de vida de la sesión del usuario y la sincronización inicial de datos.</item>
    /// <item>La navegación global y la visibilidad de ventanas modales/overlays.</item>
    /// <item>La composición de ViewModels hijos especializados (<see cref="AuthVM"/>, <see cref="ChatVM"/>, <see cref="StoreVM"/>).</item>
    /// </list>
    /// </remarks>
    public partial class MainViewModel : ViewModelBase
    {
        #region Servicios Privados Inyectados

        private readonly IAuthService _authService;
        private readonly IConversationService _conversationService;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private readonly Dispatcher _dispatcher;
        private readonly ILogger<MainViewModel> _logger;

        #endregion

        #region Definición de Logs (Optimización CA1848)

        private static readonly Action<ILogger, Exception?> s_logSyncDataError =
            LoggerMessage.Define(LogLevel.Error, new EventId(2, "SyncDataError"), "Error sincronizando datos iniciales del usuario.");

        private static readonly Action<ILogger, Exception?> s_logFullscreenImageError =
            LoggerMessage.Define(LogLevel.Error, new EventId(3, "FullscreenImageError"), "Error al cargar imagen en pantalla completa.");

        private static readonly Action<ILogger, Exception?> s_logInvalidDeleteAttempt =
            LoggerMessage.Define(LogLevel.Warning, new EventId(5, "InvalidDeleteAttempt"), "[MainVM] Intento de eliminar una conversación con ID nulo o vacío.");

        private static readonly Action<ILogger, Exception?> s_logDeleteConversationError =
            LoggerMessage.Define(LogLevel.Error, new EventId(4, "DeleteConversationError"), "Error eliminando conversación.");

        #endregion

        #region Campos de Estado Privados

        private static readonly HttpClient s_httpClient = new();
        private User? _currentUser;
        private IBrush? _userProfileBrush;
        private bool _isUserLoggedIn;
        private ConversationHistoryViewModel? _selectedConversation;
        private string _appChangelog = "Cargando...";
        private string _serverChangelog = "Cargando...";
        private string _privacyPolicyContent = "Cargando...";
        private string _termsOfServiceContent = "Cargando...";
        private string _newMemoryContent = string.Empty;
        private string _selectedUpdateHistoryTab = TabNames.UpdateHistoryApp;
        private string _selectedLegalTab = TabNames.LegalPrivacy;
        private string _selectedSettingsTab = TabNames.SettingsGeneral;
        private bool _isSpanish = true;

        #endregion

        #region ViewModels Hijos (Composición)

        /// <summary> Obtiene el ViewModel encargado de la autenticación y seguridad. </summary>
        public AuthViewModel AuthVM { get; }

        /// <summary> Obtiene el ViewModel encargado de la tienda y procesamiento de pagos. </summary>
        public StoreViewModel StoreVM { get; }

        /// <summary> Obtiene el ViewModel encargado de la lógica interactiva del chat. </summary>
        public ChatViewModel ChatVM { get; }

        #endregion

        #region Propiedades de Enlace (Bindings)

        /// <summary> Obtiene el servicio de UI para gestionar visibilidades globales desde XAML. </summary>
        public IUIService UI { get; }

        /// <summary> Indica si hay una sesión de usuario activa y validada. </summary>
        public bool IsUserLoggedIn { get => _isUserLoggedIn; set => SetProperty(ref _isUserLoggedIn, value); }

        /// <summary> Obtiene el nombre del usuario o el identificador por defecto "Ada". </summary>
        public string UserDisplayName => !string.IsNullOrWhiteSpace(_currentUser?.Name) ? _currentUser.Name : "Ada";

        /// <summary> Pincel que contiene la imagen de perfil del usuario procesada. </summary>
        public IBrush? UserProfileBrush { get => _userProfileBrush; private set => SetProperty(ref _userProfileBrush, value); }

        /// <summary> Obtiene o establece la conversación seleccionada en el historial lateral. </summary>
        public ConversationHistoryViewModel? SelectedConversation
        {
            get => _selectedConversation;
            set
            {
                if (SetProperty(ref _selectedConversation, value))
                {
                    if (value != null) { _ = LoadSelectedConversationAsync(value); }
                    else { ChatVM.Clear(); }
                }
            }
        }

        /// <summary> Texto de la nueva memoria pendiente de guardar. </summary>
        public string NewMemoryContent { get => _newMemoryContent; set => SetProperty(ref _newMemoryContent, value); }

        // --- Propiedades de Contenido Dinámico ---
        public string CurrentChangelogContent => _selectedUpdateHistoryTab == TabNames.UpdateHistoryApp ? _appChangelog : _serverChangelog;
        public string CurrentLegalContent => _selectedLegalTab == TabNames.LegalPrivacy ? _privacyPolicyContent : _termsOfServiceContent;
        public string SelectedUpdateHistoryTab { get => _selectedUpdateHistoryTab; set { if (SetProperty(ref _selectedUpdateHistoryTab, value)) { OnPropertyChanged(nameof(CurrentChangelogContent)); } } }
        public string SelectedLegalTab { get => _selectedLegalTab; set { if (SetProperty(ref _selectedLegalTab, value)) { OnPropertyChanged(nameof(CurrentLegalContent)); } } }
        public string SelectedSettingsTab { get => _selectedSettingsTab; set => SetProperty(ref _selectedSettingsTab, value); }
        public string CurrentLanguageLabel => _isSpanish ? "Español" : "English";

        public bool IsGeneralTabActive => _selectedSettingsTab == TabNames.SettingsGeneral;
        public bool IsAboutTabActive => _selectedSettingsTab == TabNames.SettingsAbout;

        #endregion

        #region Colecciones Observables

        public ObservableCollection<ConversationHistoryViewModel> ConversationHistory { get; } = [];
        public ObservableCollection<ErrorNotificationViewModel> ErrorNotifications { get; } = [];
        public ObservableCollection<MemoryViewModel> Memories { get; } = [];
        public ObservableCollection<AttachedFileViewModel> AttachedFiles => ChatVM.AttachedFiles;

        #endregion

        #region Comandos Globales

        public ICommand NewConversationCommand { get; }
        public ICommand LoadConversationCommand { get; }
        public ICommand ProcessTitleEditCommand { get; }
        public ICommand AddMemoryCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand CloseNotificationCommand { get; }
        public ICommand CloseActiveOverlayCommand { get; }
        public ICommand OpenImageFullscreenCommand { get; }
        public ICommand CloseImageFullscreenCommand { get; }
        public ICommand ToggleSettingsFlyoutCommand { get; }
        public ICommand OpenSettingsWindowCommand { get; }
        public ICommand SelectSettingsTabCommand { get; }
        public ICommand OpenSubscriptionsWindowCommand { get; }
        public ICommand OpenMemoriesWindowCommand { get; }
        public ICommand OpenUpdateHistoryWindowCommand { get; }
        public ICommand SelectUpdateHistoryTabCommand { get; }
        public ICommand OpenLegalWindowCommand { get; }
        public ICommand SelectLegalTabCommand { get; }
        public ICommand ToggleLanguageCommand { get; }
        public ICommand OpenForgotPasswordCommand { get; }
        public ICommand CloseForgotPasswordCommand { get; }
        public ICommand CloseResetPasswordViewCommand { get; }

        #endregion

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="MainViewModel"/> inyectando todas las dependencias necesarias.
        /// </summary>
        public MainViewModel(
            IAuthService authService,
            IConversationService conversationService,
            IFileStorageService fileStorageService,
            IUserService userService,
            IPaymentService paymentService,
            INotificationService notificationService,
            IStorageService storageService,
            IVoiceRecognitionService voiceRecognitionService,
            IUIService uiService,
            IChatService chatService,
            Dispatcher dispatcher,
            ILogger<MainViewModel> logger)
        {
            _authService = authService;
            _conversationService = conversationService;
            _userService = userService;
            _notificationService = notificationService;
            UI = uiService;
            _dispatcher = dispatcher;
            _logger = logger;

            // Inicialización de Hijos
            AuthVM = new AuthViewModel(authService);
            StoreVM = new StoreViewModel(paymentService, authService, notificationService);
            ChatVM = new ChatViewModel(chatService, fileStorageService, storageService, voiceRecognitionService, notificationService, conversationService, authService, dispatcher, logger);

            // Suscripción a eventos
            AuthVM.OnUserLoggedIn += HandleUserLoggedIn;
            AuthVM.OnUserLoggedOut += HandleUserLoggedOut;
            ChatVM.OnNewConversationCreated += HandleNewConversationCreated;
            _notificationService.OnShowTemporaryNotification += ShowNotification;
            _notificationService.OnShowComplexNotification += ShowComplexNotification;

            // Registro de Comandos
            NewConversationCommand = new RelayCommand(_ => { SelectedConversation = null; ChatVM.Clear(); return Task.CompletedTask; });
            LoadConversationCommand = new RelayCommand(async vm => { if (vm is ConversationHistoryViewModel cvm) { await LoadSelectedConversationAsync(cvm); } });
            ProcessTitleEditCommand = new RelayCommand(async vm => { if (vm is ConversationHistoryViewModel cvm) { await ProcessTitleEditAsync(cvm); } });
            AddMemoryCommand = new RelayCommand(async _ => await AddMemoryAsync());
            LogoutCommand = AuthVM.LogoutCommand;
            CloseNotificationCommand = new RelayCommand(vm => { if (vm is ErrorNotificationViewModel n) { _ = ErrorNotifications.Remove(n); } return Task.CompletedTask; });
            CloseActiveOverlayCommand = new RelayCommand(_ => { CloseAllOverlays(); return Task.CompletedTask; });
            OpenImageFullscreenCommand = new RelayCommand(async url => await OpenImageFullscreenAsync(url as string));
            CloseImageFullscreenCommand = new RelayCommand(_ => { UI.IsImageFullscreenOpen = false; return Task.CompletedTask; });
            ToggleSettingsFlyoutCommand = new RelayCommand(_ => { UI.IsSettingsFlyoutOpen = !UI.IsSettingsFlyoutOpen; return Task.CompletedTask; });
            OpenSettingsWindowCommand = new RelayCommand(_ => { UI.IsSettingsWindowOpen = true; UI.IsSettingsFlyoutOpen = false; return Task.CompletedTask; });
            SelectSettingsTabCommand = new RelayCommand(tab => { if (tab is string t) { SelectedSettingsTab = t; } return Task.CompletedTask; });
            OpenSubscriptionsWindowCommand = new RelayCommand(_ => { UI.IsSubscriptionsWindowOpen = true; UI.IsSettingsFlyoutOpen = false; return Task.CompletedTask; });
            OpenMemoriesWindowCommand = new RelayCommand(_ => { UI.IsMemoriesWindowOpen = true; UI.IsSettingsWindowOpen = false; return Task.CompletedTask; });
            OpenUpdateHistoryWindowCommand = new RelayCommand(_ => { UI.IsUpdateHistoryWindowOpen = true; return Task.CompletedTask; });
            SelectUpdateHistoryTabCommand = new RelayCommand(tab => { if (tab is string t) { SelectedUpdateHistoryTab = t; } return Task.CompletedTask; });
            OpenLegalWindowCommand = new RelayCommand(async _ => { UI.IsLegalWindowOpen = true; await LoadLegalDocsAsync(); });
            SelectLegalTabCommand = new RelayCommand(tab => { if (tab is string t) { SelectedLegalTab = t; } return Task.CompletedTask; });
            ToggleLanguageCommand = new RelayCommand(_ => { ToggleLanguage(); return Task.CompletedTask; });
            OpenForgotPasswordCommand = new RelayCommand(_ => { CloseAllOverlays(); UI.IsForgotPasswordViewOpen = true; return Task.CompletedTask; });
            CloseForgotPasswordCommand = new RelayCommand(_ => { UI.IsForgotPasswordViewOpen = false; return Task.CompletedTask; });
            CloseResetPasswordViewCommand = new RelayCommand(_ => { UI.IsResetPasswordViewOpen = false; return Task.CompletedTask; });
        }

        #region Lógica de Sincronización y UI

        private async Task SynchronizeUserDataAsync()
        {
            string? token = await _authService.GetCurrentUserTokenAsync();
            if (token == null)
            {
                return;
            }

            try
            {
                Task<System.Collections.Generic.List<Conversation>> conversationsTask = _conversationService.GetConversationsAsync(token);
                Task<System.Collections.Generic.List<Memory>> memoriesTask = _userService.GetMemoriesAsync(token);
                await Task.WhenAll(conversationsTask, memoriesTask);

                await _dispatcher.InvokeAsync(() =>
                {
                    ConversationHistory.Clear();
                    foreach (Conversation? c in conversationsTask.Result.OrderByDescending(x => x.CreatedAt))
                    {
                        ConversationHistory.Add(CreateConversationViewModel(c));
                    }

                    Memories.Clear();
                    foreach (Memory m in memoriesTask.Result)
                    {
                        Memories.Add(new MemoryViewModel(m, UpdateMemoryAsync, DeleteMemoryAsync));
                    }
                });
            }
            catch (Exception ex)
            {
                s_logSyncDataError(_logger, ex);
                _notificationService.ShowTemporaryNotification("Error al sincronizar datos con el servidor.", true);
            }
        }

        private async Task OpenImageFullscreenAsync(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            UI.IsImageFullscreenOpen = true;
            try
            {
                UI.FullscreenImage = await UrlToBitmapConverter.GetOrFetchImageAsync(url);
            }
            catch (Exception ex)
            {
                s_logFullscreenImageError(_logger, ex);
            }
        }

        private void CloseAllOverlays()
        {
            UI.IsSettingsFlyoutOpen = false;
            UI.IsSettingsWindowOpen = false;
            UI.IsSubscriptionsWindowOpen = false;
            UI.IsMemoriesWindowOpen = false;
            UI.IsLegalWindowOpen = false;
            UI.IsUpdateHistoryWindowOpen = false;
            UI.IsForgotPasswordViewOpen = false;
            UI.IsResetPasswordViewOpen = false;
        }

        #endregion

        #region Handlers de Ciclo de Vida

        private void HandleUserLoggedIn(User user)
        {
            _currentUser = user;
            IsUserLoggedIn = true;
            OnPropertyChanged(nameof(UserDisplayName));
            _ = LoadUserProfileBrushAsync(user.ProfilePictureUrl);
            _ = SynchronizeUserDataAsync();
        }

        private void HandleUserLoggedOut()
        {
            _currentUser = null;
            IsUserLoggedIn = false;
            ConversationHistory.Clear();
            Memories.Clear();
            ChatVM.Clear();
            CloseAllOverlays();
        }

        private void HandleNewConversationCreated(Conversation newConv)
        {
            _dispatcher.Post(() =>
            {
                ConversationHistoryViewModel vm = CreateConversationViewModel(newConv);
                ConversationHistory.Insert(0, vm);
                SelectedConversation = vm;
            });
        }

        #endregion

        public async Task InitializeAsync()
        {
            await AuthVM.TrySilentLoginAsync();
            await LoadChangelogsAsync();
        }

        private ConversationHistoryViewModel CreateConversationViewModel(Conversation c)
        {
            return new ConversationHistoryViewModel(c, LoadConversationCommand, ProcessTitleEditCommand, vm => vm.IsEditingTitle = true, DeleteConversationAsync);
        }

        private async Task DeleteConversationAsync(ConversationHistoryViewModel vm)
        {
            if (vm == null || string.IsNullOrWhiteSpace(vm.ConversationId))
            {
                s_logInvalidDeleteAttempt(_logger, null);
                return;
            }

            string? token = await _authService.GetCurrentUserTokenAsync();
            if (token == null) { return; }
            int index = ConversationHistory.IndexOf(vm);
            _ = ConversationHistory.Remove(vm);
            if (SelectedConversation == vm)
            {
                SelectedConversation = null;
            }

            try
            {
                await _conversationService.DeleteConversationAsync(token, vm.ConversationId);
            }
            catch (Exception ex)
            {
                s_logDeleteConversationError(_logger, ex);
                _dispatcher.Post(() => ConversationHistory.Insert(index, vm));
                _notificationService.ShowTemporaryNotification("No se pudo eliminar la conversación del servidor.", true);
            }
        }

        private async Task LoadSelectedConversationAsync(ConversationHistoryViewModel vm)
        {
            string? token = await _authService.GetCurrentUserTokenAsync();
            if (token != null)
            {
                await ChatVM.LoadConversationAsync(vm, token);
            }
        }

        private async Task ProcessTitleEditAsync(ConversationHistoryViewModel vm)
        {
            string? token = await _authService.GetCurrentUserTokenAsync();
            if (token != null && vm.Title != vm.OriginalTitle)
            {
                _ = await _conversationService.SaveConversationMetadataAsync(token, vm.GetModel());
            }
            vm.IsEditingTitle = false;
        }

        private async Task AddMemoryAsync()
        {
            if (string.IsNullOrWhiteSpace(NewMemoryContent))
            {
                return;
            }

            string? token = await _authService.GetCurrentUserTokenAsync();
            if (token != null)
            {
                Memory m = await _userService.AddMemoryAsync(token, NewMemoryContent);
                Memories.Add(new MemoryViewModel(m, UpdateMemoryAsync, DeleteMemoryAsync));
                NewMemoryContent = string.Empty;
            }
        }

        private async Task UpdateMemoryAsync(MemoryViewModel vm)
        {
            string? token = await _authService.GetCurrentUserTokenAsync();
            if (token != null)
            {
                await _userService.UpdateMemoryAsync(token, vm.Id, vm.Content);
            }
        }

        private async Task DeleteMemoryAsync(MemoryViewModel vm)
        {
            string? token = await _authService.GetCurrentUserTokenAsync();
            if (token != null)
            {
                await _userService.DeleteMemoryAsync(token, vm.Id);
                _ = Memories.Remove(vm);
            }
        }

        private void ShowNotification(string msg, bool isError)
        {
            _dispatcher.Post(() =>
            {
                ErrorNotificationViewModel notif = new() { Message = msg, IsCritical = isError, CloseCommand = CloseNotificationCommand };
                ErrorNotifications.Add(notif);
                if (!isError)
                {
                    _ = Task.Delay(5000).ContinueWith(_ => _dispatcher.Post(() => ErrorNotifications.Remove(notif)));
                }
            });
        }

        private void ShowComplexNotification(ErrorNotificationViewModel vm)
        {
            _dispatcher.Post(() => { vm.CloseCommand = CloseNotificationCommand; ErrorNotifications.Add(vm); });
        }

        private void ToggleLanguage()
        {
            _isSpanish = !_isSpanish;
            string code = _isSpanish ? "Es" : "En";
            if (Avalonia.Application.Current is App app)
            {
                app.SetLanguage(code);
            }

            OnPropertyChanged(nameof(CurrentLanguageLabel));
            AuthVM.RefreshTexts();
            StoreVM.RefreshTexts();
        }

        private async Task LoadChangelogsAsync()
        {
            try
            {
                _appChangelog = await LoadEmbeddedResourceAsync("changelog_app.md");
                _serverChangelog = await LoadEmbeddedResourceAsync("changelog_server.md");
            }
            catch { _appChangelog = _serverChangelog = "No disponible."; }
        }

        private async Task LoadLegalDocsAsync()
        {
            try
            {
                _privacyPolicyContent = await LoadEmbeddedResourceAsync("privacy_policy.md");
                _termsOfServiceContent = await LoadEmbeddedResourceAsync("terms_of_service.md");
            }
            catch { _privacyPolicyContent = _termsOfServiceContent = "No disponible."; }
        }

        private static async Task<string> LoadEmbeddedResourceAsync(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? fullName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
            if (fullName == null)
            {
                return "Recurso no encontrado.";
            }

            using Stream? stream = assembly.GetManifestResourceStream(fullName);
            if (stream == null)
            {
                return "Error abriendo stream.";
            }

            using StreamReader reader = new(stream);
            return await reader.ReadToEndAsync();
        }

        private async Task LoadUserProfileBrushAsync(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            try
            {
                byte[] bytes = await s_httpClient.GetByteArrayAsync(url);
                using MemoryStream ms = new(bytes);
                Bitmap bitmap = new(ms);
                _ = await _dispatcher.InvokeAsync(() => UserProfileBrush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill });
            }
            catch { }
        }

        public async Task CleanupOnExitAsync()
        {
            ChatVM.Dispose();
            AuthVM.Dispose();
            await Task.CompletedTask;
        }
    }
}