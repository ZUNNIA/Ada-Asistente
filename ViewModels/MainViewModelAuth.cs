using AsistenteVirtual.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsistenteVirtual.ViewModels
{
    public partial class MainViewModel
    {
        //==================================================================//
        // LÓGICA DE AUTENTICACIÓN (EL CONTROL DE ACCESO)                   //
        //==================================================================//

        /// <summary>
        /// Se llama una sola vez al iniciar la aplicación.
        /// Intenta un inicio de sesión silencioso y, si tiene éxito, carga el historial del usuario.
        /// Finalmente, procesa cualquier mensaje que el usuario haya enviado mientras la app se conectaba.
        /// </summary>
        private async Task InitializeLoginAsync()
        {
            IsProcessingLogin = true;
            LoginStatusMessage = "Comprobando sesión anterior...";

            CurrentUser = await _authService.SilentLoginAsync();

            if (CurrentUser != null)
            {
                IsUserLoggedIn = true;
                await LoadUserResourcesAsync();
            }

            LoginStatusMessage = string.Empty;
            IsProcessingLogin = false;

            // ADVERTENCIA:
            // Es posible que el usuario envíe un mensaje antes de que el login silencioso termine.
            // Esos mensajes se guardan en una cola. Aquí se comprueba si hay mensajes pendientes
            // y se procesan ahora que la conexión está establecida.
            if (_pendingMessagesQueue.Any())
            {
                Log.Information("[InitializeAsync] Procesando {Count} mensaje(s) encolado(s).", _pendingMessagesQueue.Count);
                while (_pendingMessagesQueue.TryDequeue(out var pendingMessage))
                {
                    var (text, files, optimisticVM) = pendingMessage;

                    if (IsUserLoggedIn)
                    {
                        await ProcessAndStreamMessageAsync(text, files, optimisticVM, text);
                    }
                    else
                    {
                        Log.Warning("[InitializeAsync] Login falló. Revirtiendo mensaje encolado: {MessageText}", text);
                        RollbackFailedUserMessage(optimisticVM, text, files);
                        _notificationService.ShowTemporaryNotification("Falló el inicio de sesión. Tu mensaje no se pudo enviar.", true);
                    }
                }
                Log.Information("[InitializeAsync] Cola de mensajes procesada.");
            }
        }

        /// <summary>
        /// Inicia el proceso de autenticación interactiva del usuario.
        /// </summary>
        private async Task LoginAsync()
        {
            IsProcessingLogin = true;
            LoginStatusMessage = "Iniciando sesión...";
            CurrentUser = await _authService.LoginAsync();
            if (CurrentUser != null)
            {
                IsUserLoggedIn = true;
                await LoadUserResourcesAsync();
                LoginStatusMessage = string.Empty;
            }
            else
            {
                LoginStatusMessage = "Inicio de sesión fallido o cancelado.";
            }
            IsProcessingLogin = false;
        }

        /// <summary>
        /// Orquesta el envío del formulario de autenticación.
        /// Llama al método de registro o de inicio de sesión según el modo actual.
        /// </summary>
        private async Task SubmitAuthFormAsync()
        {
            if (IsRegisterMode)
            {
                await RegisterWithEmailAsync();
            }
            else
            {
                await LoginWithEmailAsync();
            }
        }

        /// <summary>
        /// Orquesta el envío de la solicitud de restablecimiento de contraseña.
        /// </summary>
        private async Task SendPasswordResetLinkAsync()
        {
            if (string.IsNullOrWhiteSpace(ForgotPasswordEmail))
            {
                LoginStatusMessage = "Por favor, ingresa tu correo electrónico.";
                return;
            }

            IsProcessingLogin = true;
            LoginStatusMessage = "Enviando enlace...";
            try
            {
                await _authService.RequestPasswordResetAsync(ForgotPasswordEmail);
                OpenResetPasswordViewCommand.Execute(null);
                // Muestra un mensaje genérico por seguridad, como lo hace el backend.
                LoginStatusMessage = "Si existe una cuenta, recibirás un correo.";
            }
            catch (Exception ex)
            {
                LoginStatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                // No se cierra la ventana automáticamente para que el usuario pueda ver el mensaje.
                IsProcessingLogin = false;
            }
        }
        
        /// <summary>
        /// Orquesta el envío del formulario de restablecimiento de contraseña.
        /// Envía el token y la nueva contraseña al backend para su validación y actualización.
        /// </summary>
        private async Task SubmitResetPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(PasswordResetToken) || string.IsNullOrWhiteSpace(NewPassword))
            {
                LoginStatusMessage = "Por favor, completa todos los campos.";
                return;
            }

            IsProcessingLogin = true;
            LoginStatusMessage = "Actualizando contraseña...";
            try
            {
                await _authService.ResetPasswordAsync(PasswordResetToken, NewPassword);
                LoginStatusMessage = "¡Contraseña actualizada! Ya puedes iniciar sesión.";
                // Cierra la vista de reseteo para volver al login principal.
                IsResetPasswordViewOpen = false;
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
        /// Orquesta el registro de un nuevo usuario con correo y contraseña.
        /// </summary>
        private async Task RegisterWithEmailAsync()
        {
            // Validación básica en el cliente
            if (string.IsNullOrWhiteSpace(RegisterUsername) || string.IsNullOrWhiteSpace(RegisterEmail) || string.IsNullOrWhiteSpace(Password))
            {
                LoginStatusMessage = "Por favor, completa todos los campos.";
                return;
            }

            IsProcessingLogin = true;
            LoginStatusMessage = "Creando cuenta...";
            try
            {
                CurrentUser = await _authService.RegisterWithEmailAsync(RegisterUsername, RegisterEmail, Password);
                if (CurrentUser != null)
                {
                    IsUserLoggedIn = true;
                    await LoadUserResourcesAsync();
                    LoginStatusMessage = string.Empty;
                }
                else
                {
                    LoginStatusMessage = "El registro falló. Inténtalo de nuevo.";
                }
            }
            catch (Exception ex)
            {
                // Muestra el mensaje de error que da el backend (ej. "usuario ya existe")
                LoginStatusMessage = ex.Message;
            }
            finally
            {
                IsProcessingLogin = false;
            }
        }

        /// <summary>
        /// Orquesta el inicio de sesión con correo/usuario y contraseña.
        /// </summary>
        private async Task LoginWithEmailAsync()
        {
            if (string.IsNullOrWhiteSpace(LoginIdentifier) || string.IsNullOrWhiteSpace(Password))
            {
                LoginStatusMessage = "Ingresa tu usuario/correo y contraseña.";
                return;
            }

            IsProcessingLogin = true;
            LoginStatusMessage = "Iniciando sesión...";
            try
            {
                CurrentUser = await _authService.LoginWithEmailAsync(LoginIdentifier, Password);
                if (CurrentUser != null)
                {
                    IsUserLoggedIn = true;
                    await LoadUserResourcesAsync();
                    LoginStatusMessage = string.Empty;
                }
                else
                {
                    // El servicio lanza una excepción en caso de error, pero deja esto como respaldo.
                    LoginStatusMessage = "Inicio de sesión fallido.";
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
        /// Cierra la sesión del usuario actual y limpia el estado de la aplicación.
        /// </summary>
        private async Task LogoutAsync()
        {
            await _authService.LogoutAsync();
            IsUserLoggedIn = false;
            CurrentUser = null;
            ConversationHistory.Clear();
            await CreateNewConversationAsync();
        }

        /// <summary>
        /// Carga el historial de conversaciones del usuario desde el backend.
        /// </summary>
        private async Task LoadUserResourcesAsync()
        {
            if (CurrentUser == null) return;
            string? userToken = await _authService.GetCurrentUserTokenAsync();
            if (string.IsNullOrEmpty(userToken)) return;

            var userDocument = await _backendService.GetUserDocumentAsync(userToken);
            await _dispatcher.InvokeAsync(() => ConversationHistory.Clear());

            if (userDocument?.Conversations != null)
            {
                foreach (var convEntry in userDocument.Conversations.OrderByDescending(c => c.CreatedAt))
                {
                    var vm = new ConversationHistoryViewModel(
                        convEntry.ToConversation(),
                        LoadConversationCommand,
                        ProcessTitleEditCommand,
                        (renameVM) => renameVM.IsEditingTitle = true,
                        async (deleteVM) => await DeleteConversationAsync(deleteVM)
                    );
                    await _dispatcher.InvokeAsync(() => ConversationHistory.Add(vm));
                }
            }
        }
    }
}
