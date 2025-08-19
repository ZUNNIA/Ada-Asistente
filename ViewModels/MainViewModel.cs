#pragma warning disable OPENAI001

using AsistenteVirtual.Commands;
using AsistenteVirtual.Models;
using AsistenteVirtual.Services;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using System;
using Microsoft.Extensions.Logging;

namespace AsistenteVirtual.ViewModels
{
    // DIARIO DEL DESARROLLADOR:
    // Esta clase es el "Director de Orquesta" de toda la aplicación. No toca ningún instrumento directamente
    // (no habla con las APIs, no dibuja botones), pero tiene la partitura completa y le dice a cada músico
    // (los Servicios) cuándo y cómo debe tocar. Sabe cuándo empezar la sinfonía (InitializeAsync),
    // cuándo dar paso a los solos (LoginAsync, SendMessageAsync), y cómo mantener el ritmo y la armonía
    // entre todas las partes. También es el rostro público de la orquesta, exponiendo el estado
    // de la música (las propiedades públicas) para que el "Público" (la Vista) pueda verlo y reaccionar.

    /// <summary>
    /// ViewModel principal de la aplicación. Orquesta la interacción entre los modelos,
    /// los servicios y la vista. Gestiona el estado completo de la aplicación.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        //==================================================================//
        // SERVICIOS (LOS MÚSICOS DE LA ORQUESTA)                           //
        //==================================================================//
        private readonly IAuthService _authService;
        private readonly IBackendService _backendService;
        private readonly INotificationService _notificationService;
        private readonly IStorageService _storageService;
        private readonly Dispatcher _dispatcher;
        private readonly ILogger<MainViewModel> _logger;
        private static readonly HttpClient s_httpClient = new();

        //==================================================================//
        // COLECCIONES OBSERVABLES (EL ESCENARIO)                           //
        //==================================================================//
        public ObservableCollection<ChatMessageViewModel> ChatMessages { get; } = [];
        public ObservableCollection<AttachedFileViewModel> TemporaryAttachedFiles { get; } = [];
        public ObservableCollection<AttachedFileViewModel> AttachedFiles { get; } = [];
        public ObservableCollection<ConversationHistoryViewModel> ConversationHistory { get; } = [];
        public ObservableCollection<ErrorNotificationViewModel> ErrorNotifications { get; } = [];

        //==================================================================//
        // COMANDOS (LAS ÓRDENES DEL DIRECTOR)                              //
        //==================================================================//
        public ICommand SendMessageCommand { get; }
        public ICommand AttachFileCommand { get; }
        public ICommand RemoveAttachedFileCommand { get; }
        public ICommand StopResponseCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand NewConversationCommand { get; }
        public ICommand ToggleWebSearchModeCommand { get; }
        public ICommand ToggleReasoningModeCommand { get; }
        public ICommand LoadConversationCommand { get; }
        public ICommand ProcessTitleEditCommand { get; }
        public ICommand ToggleQuickModeCommand { get; }
        public ICommand ToggleSettingsFlyoutCommand { get; }
        public ICommand CloseSettingsFlyoutCommand { get; }
        public ICommand OpenSettingsWindowCommand { get; }
        public ICommand CloseSettingsWindowCommand { get; }
        public ICommand SelectSettingsTabCommand { get; }
        public ICommand ToggleLoginRegisterCommand { get; }
        public ICommand SubmitAuthFormCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }
        public ICommand OpenForgotPasswordCommand { get; }
        public ICommand CloseForgotPasswordCommand { get; }
        public ICommand SendPasswordResetLinkCommand { get; }
        public ICommand OpenSubscriptionsWindowCommand { get; }
        public ICommand CloseSubscriptionsWindowCommand { get; }
        public ICommand OpenResetPasswordViewCommand { get; }
        public ICommand SubmitResetPasswordCommand { get; }
        public ICommand CloseResetPasswordViewCommand { get; }
        public ICommand OpenUpdateHistoryWindowCommand { get; }
        public ICommand CloseUpdateHistoryWindowCommand { get; }
        public ICommand SelectUpdateHistoryTabCommand { get; }

        /// <summary>
        /// Inicializa una nueva instancia de la clase MainViewModel.
        /// Aquí es donde el Director de Orquesta reúne a sus músicos y prepara la partitura.
        /// </summary>
        /// <param name="authService">El servicio para gestionar la autenticación (el "Portero").</param>
        /// <param name="backendService">El servicio para hablar con el servidor (el "Embajador").</param>
        /// <param name="notificationService">El servicio para mostrar notificaciones (el "Mensajero").</param>
        /// <param name="storageService">El servicio para acceder a archivos (el "Archivista").</param>
        /// <param name="dispatcher">El dispatcher de la UI para sincronizar con el hilo principal.</param>
        public MainViewModel(IAuthService authService, IBackendService backendService, INotificationService notificationService, IStorageService storageService, Dispatcher dispatcher, ILogger<MainViewModel> logger)
        {
            _authService = authService;
            _backendService = backendService;
            _notificationService = notificationService;
            _storageService = storageService;
            _dispatcher = dispatcher;
            _logger = logger;

            // Se suscribe a los eventos de los músicos para saber cuándo actuar.
            ChatMessages.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsChatEmpty));
            TemporaryAttachedFiles.CollectionChanged += (s, e) => (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            _notificationService.OnShowTemporaryNotification += ShowNotification;

            // Se asignan las acciones a las órdenes del director.
            SendMessageCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => CanSendMessage());
            AttachFileCommand = new RelayCommand(async _ => await AttachFileAsync());
            StopResponseCommand = new RelayCommand(async _ => await StopResponseAsync(), _ => IsAssistantResponding);
            LoginCommand = new RelayCommand(async _ => await LoginAsync());
            LogoutCommand = new RelayCommand(async _ => await LogoutAsync());
            NewConversationCommand = new RelayCommand(async _ => await CreateNewConversationAsync());
            ToggleWebSearchModeCommand = new RelayCommand(_ => { IsWebSearchModeActive = !IsWebSearchModeActive; return Task.CompletedTask; });
            ToggleReasoningModeCommand = new RelayCommand(_ => { ToggleReasoningMode(); return Task.CompletedTask; });
            ToggleQuickModeCommand = new RelayCommand(_ => { ToggleQuickMode(); return Task.CompletedTask; });
            LoadConversationCommand = new RelayCommand(async vm => await LoadSelectedConversationAsync(vm as ConversationHistoryViewModel));
            ProcessTitleEditCommand = new RelayCommand(async vm => { if (vm is ConversationHistoryViewModel conversationVM) await ProcessTitleEditAsync(conversationVM); });
            RemoveAttachedFileCommand = new RelayCommand(async vm => { if (vm is AttachedFileViewModel fileVM) await RemoveAttachedFileAsync(fileVM); });
            ToggleSettingsFlyoutCommand = new RelayCommand(_ => { IsSettingsFlyoutOpen = !IsSettingsFlyoutOpen; return Task.CompletedTask; });
            CloseSettingsFlyoutCommand = new RelayCommand(_ => { IsSettingsFlyoutOpen = false; return Task.CompletedTask; }, _ => IsSettingsFlyoutOpen);
            OpenSettingsWindowCommand = new RelayCommand(_ => { IsSettingsWindowOpen = true; IsSettingsFlyoutOpen = false; return Task.CompletedTask; });
            CloseSettingsWindowCommand = new RelayCommand(_ => { IsSettingsWindowOpen = false; return Task.CompletedTask; });
            SelectSettingsTabCommand = new RelayCommand(tab => { if (tab is string tabName) SelectedSettingsTab = tabName; return Task.CompletedTask; });
            ToggleLoginRegisterCommand = new RelayCommand(_ => { IsRegisterMode = !IsRegisterMode; return Task.CompletedTask; });
            SubmitAuthFormCommand = new RelayCommand(async _ => await SubmitAuthFormAsync());
            TogglePasswordVisibilityCommand = new RelayCommand(_ => { IsPasswordVisible = !IsPasswordVisible; return Task.CompletedTask; });
            OpenForgotPasswordCommand = new RelayCommand(_ => { IsForgotPasswordViewOpen = true; return Task.CompletedTask; });
            CloseForgotPasswordCommand = new RelayCommand(_ => { IsForgotPasswordViewOpen = false; return Task.CompletedTask; });
            SendPasswordResetLinkCommand = new RelayCommand(async _ => await SendPasswordResetLinkAsync());
            OpenSubscriptionsWindowCommand = new RelayCommand(_ => { IsSubscriptionsWindowOpen = true; IsSettingsFlyoutOpen = false; return Task.CompletedTask; });
            CloseSubscriptionsWindowCommand = new RelayCommand(_ => { IsSubscriptionsWindowOpen = false; return Task.CompletedTask; });
            OpenResetPasswordViewCommand = new RelayCommand(_ => { IsForgotPasswordViewOpen = false; IsResetPasswordViewOpen = true; LoginStatusMessage = "Revisa tu correo y pega el token que recibiste."; return Task.CompletedTask; });
            SubmitResetPasswordCommand = new RelayCommand(async _ => await SubmitResetPasswordAsync());
            CloseResetPasswordViewCommand = new RelayCommand(_ => { IsResetPasswordViewOpen = false; LoginStatusMessage = string.Empty; return Task.CompletedTask; });
            OpenUpdateHistoryWindowCommand = new RelayCommand(_ => { IsUpdateHistoryWindowOpen = true; return Task.CompletedTask; });
            CloseUpdateHistoryWindowCommand = new RelayCommand(_ => { IsUpdateHistoryWindowOpen = false; return Task.CompletedTask; });
            SelectUpdateHistoryTabCommand = new RelayCommand(tab => { if (tab is string tabName) SelectedUpdateHistoryTab = tabName; return Task.CompletedTask; });
        }
        
        /// <summary>
        /// Método de inicialización asíncrona. Se llama después de que la ventana
        /// se ha cargado para realizar operaciones de larga duración de forma segura.
        /// </summary>
        public async Task InitializeAsync()
        {
            _logger.LogInformation("InitializeAsync ha comenzado.");
            await InitializeLoginAsync(); 
            await LoadChangelogsAsync();
            _logger.LogInformation("InitializeAsync ha finalizado.");
        }
    }
}