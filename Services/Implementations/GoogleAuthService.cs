using AsistenteVirtual.Models;
using AsistenteVirtual.Services.Interfaces;
using GitCredentialManager;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services.Implementations
{
    /// <summary>
    /// Servicio de gestión de identidad y seguridad encargado del ciclo de vida de la sesión del usuario.
    /// </summary>
    /// <remarks>
    /// Esta clase implementa el flujo de "OAuth 2.0 para aplicaciones de escritorio" mediante un servidor HTTP local (Loopback).
    /// Además, gestiona la persistencia segura de tokens utilizando el Administrador de Credenciales del Sistema Operativo 
    /// como prioridad, con un sistema de respaldo (fallback) basado en archivos locales cifrados mediante DPAPI.
    /// </remarks>
    public class GoogleAuthService : IAuthService
    {
        #region Campos de Configuración y Estado

        private HttpListener? _activeListener;
        private const string _environment = "staging";

        private static readonly Dictionary<string, string> _authServiceBaseUrls = new()
        {
            { "staging", "https://auth-service-staging-24416219573.us-south1.run.app" },
            { "production", "https://auth-service-production-24416219573.us-south1.run.app" }
        };
        private readonly string _authServiceBaseUrl = _authServiceBaseUrls[_environment];

        private const string LocalRedirectUri = "http://127.0.0.1:5005/";
        private const string StoreNamespace = "Ada";
        private const string ServiceName = "Ada://session";
        private const string AccountKey = "user_session";

        private readonly HttpClient _httpClient;
        private readonly IDataProtectionProvider _protectionProvider;
        private readonly ICredentialStore? _store;
        private readonly bool _useOsCredentialStore;
        private string? _sessionToken;

        /// <summary>
        /// Obtiene el perfil del usuario autenticado actualmente en la sesión.
        /// </summary>
        /// <value>Instancia de <see cref="User"/> o null si no hay una sesión activa.</value>
        public User? CurrentUser { get; private set; }

        #endregion

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="GoogleAuthService"/> y configura el almacenamiento de credenciales.
        /// </summary>
        /// <param name="protectionProvider">Proveedor de protección de datos para el cifrado de tokens en disco.</param>
        /// <param name="httpClient">Cliente HTTP para la comunicación con el microservicio de autenticación.</param>
        public GoogleAuthService(IDataProtectionProvider protectionProvider, HttpClient httpClient)
        {
            _protectionProvider = protectionProvider;
            _httpClient = httpClient;
            try
            {
                // Intentamos inicializar el Git Credential Manager para usar el almacén nativo (Windows Vault / macOS Keychain)
                _store = CredentialManager.Create(StoreNamespace);
                _ = _store.Get(ServiceName, "health_check");
                _useOsCredentialStore = true;
                Log.Information("[Auth] Administrador de credenciales nativo detectado y configurado.");
            }
            catch (Exception ex) when (ex.Message.Contains("No credential store") || ex.Message.Contains("git.exe"))
            {
                _store = null;
                _useOsCredentialStore = false;
                Log.Warning("[Auth] No se pudo acceder al almacén nativo. Se utilizará cifrado DPAPI local.");
            }
        }

        #region Flujos de Inicio de Sesión

        /// <summary>
        /// Orquesta el flujo de inicio de sesión interactivo abriendo el navegador del sistema.
        /// </summary>
        /// <returns>El objeto <see cref="User"/> autenticado o null si el proceso fue cancelado.</returns>
        /// <remarks>
        /// Levanta un <see cref="HttpListener"/> temporal en el puerto 5005 para capturar el token JWT 
        /// enviado por el backend tras la redirección de Google.
        /// </remarks>
        /// <exception cref="HttpListenerException">Lanzada si el puerto local está ocupado.</exception>
        public async Task<User?> LoginAsync()
        {
            Log.Information("[Auth] Iniciando petición de login interactivo.");

            if (_activeListener != null)
            {
                try { _activeListener.Abort(); } catch { }
                _activeListener = null;
            }

            string? receivedToken = null;

            try
            {
                using HttpListener listener = new();
                _activeListener = listener;
                listener.Prefixes.Add(LocalRedirectUri);
                listener.Start();

                // Lanzar navegador hacia el microservicio de autenticación
                string backendLoginUrl = $"{_authServiceBaseUrl}/login/google";
                _ = Process.Start(new ProcessStartInfo(backendLoginUrl) { UseShellExecute = true });

                // Esperamos la llegada del token desde el navegador
                HttpListenerContext context = await listener.GetContextAsync();
                receivedToken = context.Request.QueryString.Get("token");

                await RespondToBrowserAndCloseListener(context);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Auth] Falló la escucha del servidor local de autenticación.");
                return null;
            }
            finally
            {
                if (_activeListener != null && !_activeListener.IsListening) { _activeListener = null; }
            }

            return await CompleteLoginFlow(receivedToken);
        }

        /// <summary>
        /// Intenta recuperar una sesión previa sin intervención del usuario.
        /// </summary>
        /// <returns>El usuario validado si el token guardado aún es válido; de lo contrario, null.</returns>
        public async Task<User?> SilentLoginAsync()
        {
            string? storedToken = await LoadSessionTokenAsync();
            if (string.IsNullOrWhiteSpace(storedToken)) { return null; }

            Log.Information("[Auth] Intentando validación de sesión persistente.");
            return await CompleteLoginFlow(storedToken);
        }

        /// <summary>
        /// Realiza el registro de una nueva cuenta mediante credenciales tradicionales.
        /// </summary>
        /// <param name="username">Nombre de usuario.</param>
        /// <param name="email">Correo electrónico.</param>
        /// <param name="password">Contraseña en texto plano.</param>
        /// <returns>El usuario creado y logueado.</returns>
        public async Task<User?> RegisterWithEmailAsync(string username, string email, string password)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"{_authServiceBaseUrl}/register", new { username, email, password });
            AuthTokenResponse result = await HandleAuthResponse(response);
            return await CompleteLoginFlow(result.Token);
        }

        /// <summary>
        /// Autentica al usuario mediante correo y contraseña.
        /// </summary>
        /// <param name="identifier">Nombre de usuario o correo.</param>
        /// <param name="password">Contraseña.</param>
        /// <returns>Instancia de <see cref="User"/> si las credenciales son correctas.</returns>
        public async Task<User?> LoginWithEmailAsync(string identifier, string password)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"{_authServiceBaseUrl}/login", new { identifier, password });
            AuthTokenResponse result = await HandleAuthResponse(response);
            return await CompleteLoginFlow(result.Token);
        }

        /// <summary>
        /// Termina la sesión actual y purga el token de los almacenes seguros del sistema.
        /// </summary>
        public async Task LogoutAsync()
        {
            Log.Information("[Auth] Cerrando sesión y limpiando almacenes de tokens.");
            _sessionToken = null;
            CurrentUser = null;

            if (_useOsCredentialStore && _store != null)
            {
                _ = await Task.Run(() => _store.Remove(ServiceName, AccountKey));
            }
            else if (File.Exists(GetTokenFilePath()))
            {
                File.Delete(GetTokenFilePath());
            }
        }

        /// <summary>
        /// Devuelve el token JWT de la sesión vigente para autorizar peticiones HTTP.
        /// </summary>
        /// <returns>El string del token o null si no hay sesión.</returns>
        public Task<string?> GetCurrentUserTokenAsync()
        {
            return Task.FromResult(_sessionToken);
        }

        #endregion

        #region Gestión de Contraseñas

        /// <summary>
        /// Solicita al backend el envío de un correo de recuperación de contraseña.
        /// </summary>
        /// <param name="email">Correo del usuario afectado.</param>
        public async Task RequestPasswordResetAsync(string email)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"{_authServiceBaseUrl}/forgot-password", new { email });
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(error);
            }
        }

        /// <summary>
        /// Actualiza la contraseña del usuario utilizando un token de recuperación válido.
        /// </summary>
        /// <param name="token">Token de reseteo recibido por email.</param>
        /// <param name="newPassword">Nueva contraseña elegida.</param>
        public async Task ResetPasswordAsync(string token, string newPassword)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"{_authServiceBaseUrl}/reset-password", new { token, new_password = newPassword });
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(error);
            }
        }

        #endregion

        #region Helpers de Criptografía y Validación

        /// <summary>
        /// Valida la integridad y firma del token JWT utilizando el conjunto de claves públicas del servidor.
        /// </summary>
        /// <param name="token">El token JWT a validar.</param>
        /// <returns>Un objeto <see cref="User"/> con los claims extraídos del token.</returns>
        /// <exception cref="SecurityTokenException">Lanzada si la firma es inválida o el token ha expirado.</exception>
        private async Task<User> ValidateAndDecodeJwt(string token)
        {
            string jwksUrl = $"{_authServiceBaseUrl}/.well-known/jwks.json";
            string jwksJson = await _httpClient.GetStringAsync(jwksUrl);

            JsonWebKeySet jwks = new(jwksJson);
            IList<SecurityKey> signingKeys = jwks.GetSigningKeys();

            string[] validIssuers =
            [
                "https://auth-service-staging-24416219573.us-south1.run.app",
                "https://auth-service-production-24416219573.us-south1.run.app"
            ];

            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = true,
                ValidIssuers = validIssuers,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            ClaimsPrincipal principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _);

            return new User
            {
                Id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                Name = principal.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
                Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                ProfilePictureUrl = principal.FindFirst("picture")?.Value ?? string.Empty,
                LatestAppVersion = principal.FindFirst("app_version")?.Value ?? string.Empty
            };
        }

        /// <summary>
        /// Finaliza el proceso de login validando el token y persistiendo la sesión.
        /// </summary>
        private async Task<User?> CompleteLoginFlow(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) { return null; }

            try
            {
                CurrentUser = await ValidateAndDecodeJwt(token);
                _sessionToken = token;
                await SaveSessionTokenAsync(token);
                return CurrentUser;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Auth] El token de sesión no es válido. Limpiando recursos.");
                await LogoutAsync();
                return null;
            }
        }

        /// <summary>
        /// Envía una confirmación visual al navegador del usuario tras capturar el token.
        /// </summary>
        private static async Task RespondToBrowserAndCloseListener(HttpListenerContext context)
        {
            string responseString = "<html><body style='font-family:sans-serif;text-align:center;'><h1>¡Éxito!</h1><p>Vuelve a la aplicación Ada.</p></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.OutputStream.Close();
        }

        private static async Task<AuthTokenResponse> HandleAuthResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(error);
            }
            return await response.Content.ReadFromJsonAsync<AuthTokenResponse>() ?? throw new InvalidOperationException();
        }

        #endregion

        #region Almacenamiento Persistente

        /// <summary>
        /// Guarda el token de sesión cifrado en el almacén de mayor seguridad disponible.
        /// </summary>
        private async Task SaveSessionTokenAsync(string token)
        {
            if (_useOsCredentialStore && _store != null)
            {
                await Task.Run(() => _store.AddOrUpdate(ServiceName, AccountKey, token));
            }
            else
            {
                IDataProtector protector = _protectionProvider.CreateProtector("Ada.Auth.v1");
                string protectedToken = protector.Protect(token);
                await File.WriteAllTextAsync(GetTokenFilePath(), protectedToken);
            }
        }

        /// <summary>
        /// Recupera el token guardado del almacén seguro.
        /// </summary>
        private async Task<string?> LoadSessionTokenAsync()
        {
            if (_useOsCredentialStore && _store != null)
            {
                ICredential cred = await Task.Run(() => _store.Get(ServiceName, AccountKey));
                if (cred != null) { return cred.Password; }
            }

            string path = GetTokenFilePath();
            if (File.Exists(path))
            {
                try
                {
                    string protectedToken = await File.ReadAllTextAsync(path);
                    return _protectionProvider.CreateProtector("Ada.Auth.v1").Unprotect(protectedToken);
                }
                catch { File.Delete(path); }
            }
            return null;
        }

        private static string GetTokenFilePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ada");
            _ = Directory.CreateDirectory(dir);
            return Path.Combine(dir, "session.token");
        }

        private sealed class AuthTokenResponse { public string Token { get; set; } = string.Empty; }

        #endregion
    }
}