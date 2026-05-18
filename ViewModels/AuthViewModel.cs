using AsistenteVirtual.Commands;
using AsistenteVirtual.Models;
using AsistenteVirtual.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// ViewModel especializado en la orquestación del ciclo de vida de la identidad del usuario.
    /// </summary>
    /// <remarks>
    /// Gestiona de forma centralizada los flujos de inicio de sesión (interactivo y silencioso), 
    /// registro de nuevas cuentas y recuperación de credenciales. Implementa mecanismos de 
    /// cancelación para evitar inconsistencias en la UI durante peticiones de red concurrentes.
    /// </remarks>
    public class AuthViewModel : ViewModelBase, IDisposable
    {
        private readonly IAuthService _authService;

        /// <summary> Se dispara cuando un usuario ha sido validado y autenticado correctamente. </summary>
        public event Action<User>? OnUserLoggedIn;

        /// <summary> Se dispara cuando la sesión actual ha sido destruida o cerrada. </summary>
        public event Action? OnUserLoggedOut;

        #region Campos de Estado Privados

        private CancellationTokenSource? _uiLoginCts;
        private bool _isRegisterMode;
        private string _loginIdentifier = string.Empty;
        private string _registerUsername = string.Empty;
        private string _registerEmail = string.Empty;
        private string _password = string.Empty;
        private bool _isPasswordVisible;
        private string _loginStatusMessage = string.Empty;
        private bool _isProcessingLogin;
        private string _forgotPasswordEmail = string.Empty;
        private string _passwordResetToken = string.Empty;
        private string _newPassword = string.Empty;

        #endregion

        #region Propiedades de Enlace (Bindings)

        /// <summary>
        /// Define si la vista debe mostrar el formulario de registro (true) o el de login (false).
        /// </summary>
        public bool IsRegisterMode
        {
            get => _isRegisterMode;
            set
            {
                if (SetProperty(ref _isRegisterMode, value))
                {
                    OnPropertyChanged(nameof(AuthWindowTitle));
                    OnPropertyChanged(nameof(AuthButtonText));
                    OnPropertyChanged(nameof(AuthToggleQuestionText));
                    OnPropertyChanged(nameof(AuthToggleActionText));
                }
            }
        }

        /// Título dinámico de la ventana de autenticación.
        public string AuthWindowTitle => IsRegisterMode ? App.GetString("Auth_Title_Register") : App.GetString("Auth_Title_Login");

        /// Texto del botón principal de acción.
        public string AuthButtonText => IsRegisterMode ? App.GetString("Auth_Btn_Register") : App.GetString("Auth_Btn_Login");

        /// Pregunta de alternancia entre modos.
        public string AuthToggleQuestionText => IsRegisterMode ? App.GetString("Auth_Quest_HaveAcc") : App.GetString("Auth_Quest_NoAcc");

        /// Acción sugerida para cambiar de modo.
        public string AuthToggleActionText => IsRegisterMode ? App.GetString("Auth_Link_Login") : App.GetString("Auth_Link_Register");

        /// Identificador de entrada (Email o Username).
        public string LoginIdentifier { get => _loginIdentifier; set => SetProperty(ref _loginIdentifier, value); }

        /// Nombre de usuario para el registro.
        public string RegisterUsername { get => _registerUsername; set => SetProperty(ref _registerUsername, value); }

        /// Correo electrónico para el registro.
        public string RegisterEmail { get => _registerEmail; set => SetProperty(ref _registerEmail, value); }

        /// Contraseña del usuario.
        public string Password { get => _password; set => SetProperty(ref _password, value); }

        /// <summary>
        /// Controla si la contraseña se muestra en texto claro o enmascarada.
        /// </summary>
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set
            {
                if (SetProperty(ref _isPasswordVisible, value))
                {
                    OnPropertyChanged(nameof(PasswordMaskChar));
                    OnPropertyChanged(nameof(PasswordVisibilityIconPath));
                }
            }
        }

        /// Carácter utilizado para enmascarar la contraseña.
        public char PasswordMaskChar => IsPasswordVisible ? '\0' : '●';

        /// Ruta del icono para el botón de visibilidad de contraseña.
        public string PasswordVisibilityIconPath => IsPasswordVisible ? "avares://Ada/Resources/Icons/visibility-off.svg" : "avares://Ada/Resources/Icons/visibility-on.svg";

        /// Correo destino para la solicitud de recuperación.
        public string ForgotPasswordEmail { get => _forgotPasswordEmail; set => SetProperty(ref _forgotPasswordEmail, value); }

        /// Token de seguridad recibido para el reseteo.
        public string PasswordResetToken { get => _passwordResetToken; set => SetProperty(ref _passwordResetToken, value); }

        /// Nueva contraseña para establecer tras el reseteo.
        public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }

        /// Mensaje informativo o de error que se muestra al usuario.
        public string LoginStatusMessage { get => _loginStatusMessage; set => SetProperty(ref _loginStatusMessage, value); }

        /// Indica si hay una operación de autenticación en curso para mostrar indicadores de carga.
        public bool IsProcessingLogin { get => _isProcessingLogin; set => SetProperty(ref _isProcessingLogin, value); }

        #endregion

        #region Comandos

        /// Comando para iniciar el flujo interactivo de Google Login.
        public ICommand LoginCommand { get; }

        /// Comando para cerrar la sesión actual.
        public ICommand LogoutCommand { get; }

        /// Comando para alternar entre el modo de Login y Registro.
        public ICommand ToggleLoginRegisterCommand { get; }

        /// Comando para enviar el formulario de autenticación por correo.
        public ICommand SubmitAuthFormCommand { get; }

        /// Comando para alternar la visibilidad del texto de la contraseña.
        public ICommand TogglePasswordVisibilityCommand { get; }

        /// Comando para solicitar el token de reseteo por email.
        public ICommand SendPasswordResetLinkCommand { get; }

        /// Comando para enviar la nueva contraseña junto con el token.
        public ICommand SubmitResetPasswordCommand { get; }

        #endregion

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="AuthViewModel"/> configurando los comandos.
        /// </summary>
        /// <param name="authService">Servicio de autenticación inyectado.</param>
        public AuthViewModel(IAuthService authService)
        {
            _authService = authService;

            LoginCommand = new RelayCommand(async _ => await LoginAsync());
            LogoutCommand = new RelayCommand(async _ => await LogoutAsync());
            ToggleLoginRegisterCommand = new RelayCommand(_ => { IsRegisterMode = !IsRegisterMode; return Task.CompletedTask; });
            SubmitAuthFormCommand = new RelayCommand(async _ => await SubmitAuthFormAsync());
            TogglePasswordVisibilityCommand = new RelayCommand(_ => { IsPasswordVisible = !IsPasswordVisible; return Task.CompletedTask; });
            SendPasswordResetLinkCommand = new RelayCommand(async _ => await SendPasswordResetLinkAsync());
            SubmitResetPasswordCommand = new RelayCommand(async _ => await SubmitResetPasswordAsync());
        }

        /// <summary>
        /// Intenta restaurar una sesión previa sin intervención del usuario al arrancar la aplicación.
        /// </summary>
        /// <returns>Tarea que representa la operación de carga inicial.</returns>
        public async Task TrySilentLoginAsync()
        {
            IsProcessingLogin = true;
            LoginStatusMessage = App.GetString("Status_CheckSession");
            User? user = await _authService.SilentLoginAsync();

            if (user != null)
            {
                OnUserLoggedIn?.Invoke(user);
            }

            LoginStatusMessage = string.Empty;
            IsProcessingLogin = false;
        }

        /// <summary>
        /// Inicia el proceso de login interactivo, manejando la cancelación de intentos previos.
        /// </summary>
        /// <returns>Tarea que representa el flujo de login.</returns>
        private async Task LoginAsync()
        {
            _uiLoginCts?.Cancel();
            _uiLoginCts = new CancellationTokenSource();
            CancellationToken token = _uiLoginCts.Token;

            IsProcessingLogin = true;
            LoginStatusMessage = App.GetString("Status_LoggingIn");

            try
            {
                User? user = await _authService.LoginAsync();

                if (token.IsCancellationRequested) { return; }

                if (user != null)
                {
                    OnUserLoggedIn?.Invoke(user);
                    LoginStatusMessage = string.Empty;
                }
                else
                {
                    LoginStatusMessage = App.GetString("Status_LoginCancelled");
                }
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsProcessingLogin = false;
                }
            }
        }

        /// <summary>
        /// Discrimina y ejecuta la acción de autenticación correspondiente según el modo activo (Login/Registro).
        /// </summary>
        private async Task SubmitAuthFormAsync()
        {
            if (IsRegisterMode) { await RegisterWithEmailAsync(); }
            else { await LoginWithEmailAsync(); }
        }

        /// <summary>
        /// Valida y procesa la creación de una nueva cuenta.
        /// </summary>
        private async Task RegisterWithEmailAsync()
        {
            if (string.IsNullOrWhiteSpace(RegisterUsername) || string.IsNullOrWhiteSpace(RegisterEmail) || string.IsNullOrWhiteSpace(Password))
            {
                LoginStatusMessage = App.GetString("Status_FillFields");
                return;
            }

            IsProcessingLogin = true;
            LoginStatusMessage = App.GetString("Status_CreatingAcc");
            try
            {
                User? user = await _authService.RegisterWithEmailAsync(RegisterUsername, RegisterEmail, Password);
                if (user != null)
                {
                    OnUserLoggedIn?.Invoke(user);
                    LoginStatusMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                LoginStatusMessage = ex.Message;
            }
            finally
            {
                IsProcessingLogin = false;
            }
        }

        /// <summary>
        /// Valida y procesa el inicio de sesión mediante credenciales tradicionales.
        /// </summary>
        private async Task LoginWithEmailAsync()
        {
            if (string.IsNullOrWhiteSpace(LoginIdentifier) || string.IsNullOrWhiteSpace(Password))
            {
                LoginStatusMessage = App.GetString("Status_NoCreds");
                return;
            }

            IsProcessingLogin = true;
            LoginStatusMessage = App.GetString("Status_LoggingIn");
            try
            {
                User? user = await _authService.LoginWithEmailAsync(LoginIdentifier, Password);
                if (user != null)
                {
                    OnUserLoggedIn?.Invoke(user);
                    LoginStatusMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                LoginStatusMessage = ex.Message;
            }
            finally
            {
                IsProcessingLogin = false;
            }
        }

        /// <summary>
        /// Finaliza la sesión del usuario y notifica al sistema.
        /// </summary>
        private async Task LogoutAsync()
        {
            await _authService.LogoutAsync();
            OnUserLoggedOut?.Invoke();
        }

        /// <summary>
        /// Solicita al backend el envío de un correo de recuperación.
        /// </summary>
        private async Task SendPasswordResetLinkAsync()
        {
            if (string.IsNullOrWhiteSpace(ForgotPasswordEmail)) { return; }
            IsProcessingLogin = true;
            try
            {
                await _authService.RequestPasswordResetAsync(ForgotPasswordEmail);
                LoginStatusMessage = App.GetString("Status_ResetSent");
            }
            catch (Exception ex) { LoginStatusMessage = ex.Message; }
            finally { IsProcessingLogin = false; }
        }

        /// <summary>
        /// Envía los nuevos datos de acceso al servidor tras una recuperación.
        /// </summary>
        private async Task SubmitResetPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(PasswordResetToken) || string.IsNullOrWhiteSpace(NewPassword)) { return; }
            IsProcessingLogin = true;
            try
            {
                await _authService.ResetPasswordAsync(PasswordResetToken, NewPassword);
                LoginStatusMessage = App.GetString("Status_PassUpdated");
            }
            catch (Exception ex) { LoginStatusMessage = ex.Message; }
            finally { IsProcessingLogin = false; }
        }

        /// <summary>
        /// Notifica cambios en todas las propiedades dependientes de la localización para actualizar la UI dinámicamente.
        /// </summary>
        public void RefreshTexts()
        {
            OnPropertyChanged(nameof(AuthWindowTitle));
            OnPropertyChanged(nameof(AuthButtonText));
            OnPropertyChanged(nameof(AuthToggleQuestionText));
            OnPropertyChanged(nameof(AuthToggleActionText));
            LoginStatusMessage = string.Empty;
        }

        /// <summary>
        /// Libera los recursos de cancelación y limpia eventos.
        /// </summary>
        public void Dispose()
        {
            _uiLoginCts?.Cancel();
            _uiLoginCts?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}