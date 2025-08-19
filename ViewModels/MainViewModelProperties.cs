using AsistenteVirtual.Commands;
using AsistenteVirtual.Models;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using System.Threading;
using System;

namespace AsistenteVirtual.ViewModels
{
    public partial class MainViewModel
    {
        //==================================================================//
        // CONFIGURACIÓN Y ESTADO INTERNO (LA PARTITURA)                    //
        //==================================================================//
        private readonly List<string> _supportedVisionExtensions = new() { ".png", ".jpeg", ".jpg", ".webp", ".gif" };
        private readonly List<string> _supportedFileSearchExtensions = new() { ".c", ".cpp", ".css", ".go", ".html", ".java", ".js", ".json", ".md", ".pdf", ".php", ".pptx", ".py", ".rb", ".sh", ".tex", ".ts", ".txt", ".xml", ".docx" };
        private bool _isUserLoggedIn;
        private bool _isRegisterMode = true;
        private string _loginIdentifier = string.Empty;
        private string _registerUsername = string.Empty;
        private string _registerEmail = string.Empty;
        private string _password = string.Empty;
        private bool _isPasswordVisible = false;
        private bool _isForgotPasswordViewOpen;
        private string _forgotPasswordEmail = string.Empty;
        private bool _isResetPasswordViewOpen;
        private string _passwordResetToken = string.Empty;
        private string _newPassword = string.Empty;
        private bool _isAssistantResponding;
        private bool _isWebSearchModeActive;
        private bool _isReasoningModeActive;
        private bool _isQuickModeActive;
        private bool _isProcessingLogin;
        private bool _isLoadingConversation;
        private bool _isSettingsFlyoutOpen;
        private bool _isSettingsWindowOpen;
        private string _selectedSettingsTab = "General";
        private bool _isSubscriptionsWindowOpen;
        private string _userInputText = string.Empty;
        private string _loginStatusMessage = string.Empty;
        private User? _currentUser;
        private IBrush? _userProfileBrush;
        private ConversationHistoryViewModel? _currentConversationHistoryViewModel;
        private bool _isPendingNewConversation = true;
        private CancellationTokenSource? _responseCts;
        private readonly Queue<Tuple<string, List<AttachedFileViewModel>, ChatMessageViewModel>> _pendingMessagesQueue = new();
        private const string DefaultAppIcon = "avares://Ada/Resources/Icons/ada.png";
        private const string DefaultAppName = "Ada";
        private bool _isUpdateHistoryWindowOpen;
        private string _selectedUpdateHistoryTab = "App";
        private string _appChangelog = "Cargando...";
        private string _serverChangelog = "Cargando...";

        #region PROPIEDADES PÚBLICAS (EL ESTADO DE LA ORQUESTA)

        public bool IsUserLoggedIn { get => _isUserLoggedIn; set => SetProperty(ref _isUserLoggedIn, value); }
        public bool IsAssistantResponding { get => _isAssistantResponding; set { if (SetProperty(ref _isAssistantResponding, value)) { (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged(); (StopResponseCommand as RelayCommand)?.RaiseCanExecuteChanged(); } } }
        public bool IsWebSearchModeActive { get => _isWebSearchModeActive; set => SetProperty(ref _isWebSearchModeActive, value); }
        public bool IsReasoningModeActive { get => _isReasoningModeActive; set => SetProperty(ref _isReasoningModeActive, value); }
        public bool IsQuickModeActive { get => _isQuickModeActive; set => SetProperty(ref _isQuickModeActive, value); }
        public string UserInputText { get => _userInputText; set { if (SetProperty(ref _userInputText, value)) { (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged(); } } }
        public IBrush? UserProfileBrush { get => _userProfileBrush; private set => SetProperty(ref _userProfileBrush, value); }
        public User? CurrentUser { get => _currentUser; private set { if (SetProperty(ref _currentUser, value)) { OnPropertyChanged(nameof(UserDisplayName)); _ = LoadUserProfileBrushAsync(CurrentUser?.ProfilePictureUrl); } } }
        public string UserDisplayName => CurrentUser?.Name ?? DefaultAppName;
        public bool IsChatEmpty => ChatMessages.Count == 0;
        public bool IsUserInputEnabled => !IsAssistantResponding;
        public bool IsProcessingLogin { get => _isProcessingLogin; set => SetProperty(ref _isProcessingLogin, value); }
        public bool IsLoadingConversation { get => _isLoadingConversation; set => SetProperty(ref _isLoadingConversation, value); }
        public bool IsSettingsFlyoutOpen { get => _isSettingsFlyoutOpen; set => SetProperty(ref _isSettingsFlyoutOpen, value); }
        public bool IsSettingsWindowOpen { get => _isSettingsWindowOpen; set => SetProperty(ref _isSettingsWindowOpen, value); }
        public bool IsSubscriptionsWindowOpen { get => _isSubscriptionsWindowOpen; set => SetProperty(ref _isSubscriptionsWindowOpen, value); }
        public string LoginStatusMessage { get => _loginStatusMessage; set => SetProperty(ref _loginStatusMessage, value); }
        public bool IsRegisterMode { get => _isRegisterMode; set { if (SetProperty(ref _isRegisterMode, value)) { OnPropertyChanged(nameof(AuthWindowTitle)); OnPropertyChanged(nameof(AuthButtonText)); OnPropertyChanged(nameof(AuthToggleQuestionText)); OnPropertyChanged(nameof(AuthToggleActionText)); } } }
        public string AuthWindowTitle => IsRegisterMode ? "Crear una cuenta" : "Iniciar Sesión";
        public string AuthButtonText => IsRegisterMode ? "Crear cuenta" : "Iniciar Sesión";
        public string AuthToggleQuestionText => IsRegisterMode ? "¿Ya tienes una cuenta?" : "¿No tienes una cuenta?";
        public string AuthToggleActionText => IsRegisterMode ? "Inicia sesión" : "Regístrate";
        public string LoginIdentifier { get => _loginIdentifier; set => SetProperty(ref _loginIdentifier, value); }
        public string RegisterUsername { get => _registerUsername; set => SetProperty(ref _registerUsername, value); }
        public string RegisterEmail { get => _registerEmail; set => SetProperty(ref _registerEmail, value); }
        public string Password { get => _password; set => SetProperty(ref _password, value); }
        public bool IsPasswordVisible { get => _isPasswordVisible; set { if (SetProperty(ref _isPasswordVisible, value)) { OnPropertyChanged(nameof(PasswordMaskChar)); OnPropertyChanged(nameof(PasswordVisibilityIconPath)); } } }
        public char PasswordMaskChar => IsPasswordVisible ? '\0' : '●';
        public string PasswordVisibilityIconPath => IsPasswordVisible ? "avares://Ada/Resources/Icons/adjuntar.svg" : "avares://Ada/Resources/Icons/eliminar.svg";
        public bool IsForgotPasswordViewOpen { get => _isForgotPasswordViewOpen; set => SetProperty(ref _isForgotPasswordViewOpen, value); }
        public string ForgotPasswordEmail { get => _forgotPasswordEmail; set => SetProperty(ref _forgotPasswordEmail, value); }
        public bool IsResetPasswordViewOpen { get => _isResetPasswordViewOpen; set => SetProperty(ref _isResetPasswordViewOpen, value); }
        public string PasswordResetToken { get => _passwordResetToken; set => SetProperty(ref _passwordResetToken, value); }
        public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
        public string SelectedSettingsTab { get => _selectedSettingsTab; set { if (SetProperty(ref _selectedSettingsTab, value)) { OnPropertyChanged(nameof(IsGeneralTabActive)); OnPropertyChanged(nameof(IsAboutTabActive)); } } }
        public bool IsGeneralTabActive => SelectedSettingsTab == "General";
        public bool IsAboutTabActive => SelectedSettingsTab == "About";
        public List<string> EssentialPlanFeatures { get; } = new List<string>
        {
            "Hasta 2,100 mensajes con gpt-4.1-nano (70 diarios)",
            "Hasta 900 mensajes con gpt-4.1-mini (30 diarios)",
            "Hasta 300 mensajes con o3-mini (10 diarios)",
            "Hasta 300 búsquedas web (10 diarias)"
        };
        public List<string> PlusPlanFeatures { get; } = new List<string>
        {
            "Hasta 6,300 mensajes con gpt-4.1-nano (210 diarios)",
            "Hasta 2,700 mensajes con gpt-4.1-mini (90 diarios)",
            "Hasta 900 mensajes con o3-mini (30 diarios)",
            "Hasta 900 búsquedas web (30 diarias)"
        };
        public bool IsUpdateHistoryWindowOpen { get => _isUpdateHistoryWindowOpen; set => SetProperty(ref _isUpdateHistoryWindowOpen, value); }
        public string SelectedUpdateHistoryTab { get => _selectedUpdateHistoryTab; set { if (SetProperty(ref _selectedUpdateHistoryTab, value)) { OnPropertyChanged(nameof(IsAppHistoryTabActive)); OnPropertyChanged(nameof(IsServerHistoryTabActive)); OnPropertyChanged(nameof(CurrentChangelogContent)); } } }
        public bool IsAppHistoryTabActive => SelectedUpdateHistoryTab == "App";
        public bool IsServerHistoryTabActive => SelectedUpdateHistoryTab == "Servidor";
        public string AppChangelog { get => _appChangelog; set => SetProperty(ref _appChangelog, value); }
        public string ServerChangelog { get => _serverChangelog; set => SetProperty(ref _serverChangelog, value); }
        public string CurrentChangelogContent => SelectedUpdateHistoryTab == "App" ? AppChangelog : ServerChangelog;

        #endregion
    }
}
