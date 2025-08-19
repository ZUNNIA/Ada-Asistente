using AsistenteVirtual.Models;
using GitCredentialManager;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services.Implementations
{
    /// <summary>
    /// Implementación de IAuthService para la autenticación a través de un backend seguro.
    /// Gestiona un flujo de autorización que redirige al usuario al navegador para el inicio
    /// de sesión y recibe un token de sesión (JWT) desde el servidor.
    /// </summary>
    public class GoogleAuthService : IAuthService
    {
        // --- Constantes ---
        // URL del Cloud Run que gestiona el registro y login con correo.
        private const string AuthServiceBaseUrl = "https://auth-service-802177958692.us-south1.run.app"; 
        // URL de la función de Cloud Run que inicia el proceso de login.
        private const string BackendLoginUrl = "https://auth-service-802177958692.us-south1.run.app/login/google";
        // URL local donde la app escuchará para recibir el token del backend.
        private const string LocalRedirectUri = "http://127.0.0.1:5005/";
        private const string StoreNamespace = "Ada";
        private const string ServiceName = "Ada://session";
        private const string AccountKey = "user_session";

        // --- Campos de Estado ---
        private readonly HttpClient _httpClient = new();
        private readonly IDataProtectionProvider _protectionProvider;
        private readonly ICredentialStore? _store;
        private readonly bool _useOsCredentialStore;
        private string? _sessionToken;

        /// <summary>
        /// Obtiene el objeto del usuario actualmente autenticado. Es nulo si no hay sesión activa.
        /// </summary>
        public User? CurrentUser { get; private set; }

        /// <summary>
        /// Inicializa el servicio de autenticación con el proveedor de protección de datos.
        /// </summary>
        /// <param name="protectionProvider">Servicio inyectado para cifrar y descifrar datos.</param>
        public GoogleAuthService(IDataProtectionProvider protectionProvider)
        {
            _protectionProvider = protectionProvider;
            try
            {
                // 1. Intenta inicializar el almacén de credenciales del SO.
                _store = CredentialManager.Create(StoreNamespace);
                
                // 2. Hace una "prueba" para ver si realmente funciona.
                // Esto lanzará la excepción aquí si el almacén no está configurado.
                _store.Get(ServiceName, "health_check");

                // 3. Si todo va bien, establece la bandera para usarlo.
                _useOsCredentialStore = true;
                Log.Information("Se utilizará el Administrador de Credenciales nativo del SO.");
            }
            catch (Exception ex) when (ex.Message.Contains("No credential store has been selected"))
            {
                // 4. Si la prueba falla, lo registramos y usamos el método de respaldo.
                _store = null;
                _useOsCredentialStore = false;
                Log.Warning("No se encontró un almacén de credenciales nativo del SO. Se recurrirá al almacenamiento de archivos cifrados (seguro).");
            }
        }
        
        /// <summary>
        /// Registra un nuevo usuario en el backend usando su nombre, correo y contraseña.
        /// </summary>
        /// <param name="username">El nombre de usuario para la nueva cuenta.</param>
        /// <param name="email">El correo electrónico para la nueva cuenta.</param>
        /// <param name="password">La contraseña del usuario.</param>
        /// <returns>Un objeto User con los datos del usuario si el registro es exitoso.</returns>
        /// <exception cref="Exception">Lanza una excepción con el mensaje de error del backend si el registro falla (ej. "usuario ya existe").</exception>
        public async Task<User?> RegisterWithEmailAsync(string username, string email, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{AuthServiceBaseUrl}/register", new
                {
                    username,
                    email,
                    password
                });

                if (!response.IsSuccessStatusCode)
                {
                    // Si el backend devuelve un error (ej. "usuario ya existe"), lo lee y ejecuta.
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception(error);
                }

                // Si el registro es exitoso, el backend devuelve directamente un token.
                var result = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
                if (result?.Token == null) return null;

                _sessionToken = result.Token;
                CurrentUser = DecodeJwtToUser(_sessionToken);

                if (CurrentUser != null)
                {
                    await SaveSessionTokenAsync(_sessionToken);
                }

                return CurrentUser;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AuthService] Falló el registro por correo.");
                // Propaga la excepción para que el ViewModel la muestre al usuario.
                throw;
            }
        }

        /// <summary>
        /// Inicia sesión en el backend usando un identificador (usuario o correo) y contraseña.
        /// </summary>
        /// <param name="identifier">El nombre de usuario o correo electrónico.</param>
        /// <param name="password">La contraseña del usuario.</param>
        /// <returns>Un objeto User con los datos del usuario si el inicio de sesión es exitoso.</returns>
        /// <exception cref="Exception">Lanza una excepción con el mensaje de error del backend si el inicio de sesión falla (ej. "credenciales incorrectas").</exception>
        public async Task<User?> LoginWithEmailAsync(string identifier, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{AuthServiceBaseUrl}/login", new
                {
                    identifier,
                    password
                });

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception(error);
                }

                var result = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
                if (result?.Token == null) return null;

                _sessionToken = result.Token;
                CurrentUser = DecodeJwtToUser(_sessionToken);

                if (CurrentUser != null)
                {
                    await SaveSessionTokenAsync(_sessionToken);
                }

                return CurrentUser;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AuthService] Falló el inicio de sesión por correo.");
                throw;
            }
        }

        /// <summary>
        /// Solicita un enlace de restablecimiento de contraseña al backend.
        /// </summary>
        public async Task RequestPasswordResetAsync(string email)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{AuthServiceBaseUrl}/forgot-password", new
                {
                    email
                });

                // Si el backend devuelve un error, lo lanza para que el ViewModel lo maneje.
                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception(error);
                }
                // Si tiene éxito, el backend devuelve un 200. No se necesita procesar el cuerpo.
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AuthService] Falló la solicitud de restablecimiento de contraseña.");
                throw;
            }
        }

        /// <summary>
        /// Guarda el token de sesión cifrado en un archivo local.
        /// </summary>
        private async Task SaveSessionTokenAsync(string token)
        {
            if (_useOsCredentialStore && _store != null)
            {
                // Usar el almacén del SO (si está disponible)
                await Task.Run(() => _store.AddOrUpdate(ServiceName, AccountKey, token));
                Log.Information("Token guardado de forma segura en el almacén nativo del SO.");
            }
            else
            {
                // Usar el cifrado de respaldo y guardar en un archivo
                var protector = _protectionProvider.CreateProtector("Ada.Auth.v1");
                string protectedToken = protector.Protect(token);
                await File.WriteAllTextAsync(GetTokenFilePath(), protectedToken);
                Log.Information("Token cifrado y guardado en archivo local como respaldo.");
            }
        }

        /// <summary>
        /// Inicia el proceso de autenticación interactivo a través del backend.
        /// 1. Inicia un servidor HTTP local para escuchar la respuesta del backend.
        /// 2. Abre el navegador del usuario en la URL de login del backend.
        /// 3. El usuario se autentica en Google y autoriza la aplicación.
        /// 4. El backend redirige el navegador a la URL local con el token de sesión.
        /// 5. La aplicación captura el token y finaliza el proceso.
        /// </summary>
        public async Task<User?> LoginAsync()
        {
            Log.Information("[AuthService] Iniciando proceso de inicio de sesión vía backend.");
            string? receivedToken = null;

            try
            {
                // Inicia un servidor HTTP en un hilo separado para no bloquear la UI.
                using var listener = new HttpListener();
                listener.Prefixes.Add(LocalRedirectUri);
                listener.Start();

                // Abre la URL de login del backend en el navegador por defecto del usuario.
                Process.Start(new ProcessStartInfo(BackendLoginUrl) { UseShellExecute = true });

                // Espera de forma asíncrona la petición de vuelta desde el navegador.
                HttpListenerContext context = await listener.GetContextAsync();
                HttpListenerRequest request = context.Request;

                // Extrae el token de los parámetros de la URL (ej. http://127.0.0.1:5005/?token=ey...)
                receivedToken = request.QueryString.Get("token");

                // Envía una respuesta al navegador para que el usuario sepa que puede cerrar la pestaña.
                string responseString = "<html><body style='font-family: sans-serif; text-align: center;'><h1>¡Autenticación Exitosa!</h1><p>Puedes cerrar esta ventana y volver a la aplicación.</p></body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                HttpListenerResponse response = context.Response;
                response.ContentType = "text/html; charset=utf-8";
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
                listener.Stop();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AuthService] Falló el proceso de escucha del token local.");
                return null;
            }

            // Si no se recibió un token, el proceso termina aquí.
            if (string.IsNullOrWhiteSpace(receivedToken))
            {
                Log.Warning("[AuthService] El proceso de autenticación finalizó sin recibir un token.");
                return null;
            }

            // Asigna el token a la sesión actual.
            _sessionToken = receivedToken;

            // Decodifica el token para obtener los datos del usuario.
            CurrentUser = DecodeJwtToUser(_sessionToken);

            if (CurrentUser != null)
            {
                Log.Information("[AuthService] Sesión iniciada para el usuario: {UserName} ({UserId})", CurrentUser.Name, CurrentUser.Id);
                await SaveSessionTokenAsync(_sessionToken);
            }

            return CurrentUser;
        }

        /// <summary>
        /// Obtiene el token de sesión (JWT) del usuario actual para autenticar peticiones al backend.
        /// </summary>
        public Task<string?> GetCurrentUserTokenAsync()
        {
            return Task.FromResult(_sessionToken);
        }

        /// <summary>
        /// Cierra la sesión del usuario actual, limpiando el token y los datos del usuario.
        /// </summary>
        public async Task LogoutAsync()
        {
            _sessionToken = null;
            CurrentUser = null;

            if (_useOsCredentialStore && _store != null)
            {
                await Task.Run(() => _store.Remove(ServiceName, AccountKey));
                Log.Information("Token eliminado del almacén nativo.");
            }
            else
            {
                if (File.Exists(GetTokenFilePath()))
                {
                    File.Delete(GetTokenFilePath());
                    Log.Information("Archivo de token local eliminado.");
                }
            }
        }

        /// <summary>
        /// Intenta restaurar una sesión a partir de un token cifrado guardado localmente.
        /// </summary>
        public async Task<User?> SilentLoginAsync()
        {
            string? tokenToDecode = null;

            if (_useOsCredentialStore && _store != null)
            {
                Log.Information("Intentando inicio de sesión silencioso desde el almacén nativo del SO.");
                var cred = await Task.Run(() => _store.Get(ServiceName, AccountKey));
                if (cred != null)
                {
                    tokenToDecode = cred.Password;
                }
            }
            else
            {
                Log.Information("Intentando inicio de sesión silencioso desde archivo local cifrado.");
                if (File.Exists(GetTokenFilePath()))
                {
                    string protectedToken = await File.ReadAllTextAsync(GetTokenFilePath());
                    if (!string.IsNullOrWhiteSpace(protectedToken))
                    {
                        try
                        {
                            var protector = _protectionProvider.CreateProtector("AsistenteVirtual.Auth.v1");
                            tokenToDecode = protector.Unprotect(protectedToken);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "No se pudo descifrar el token local. El archivo será eliminado.");
                            File.Delete(GetTokenFilePath()); // Elimina el token corrupto
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(tokenToDecode))
            {
                Log.Information("No se encontró un token guardado para el inicio de sesión silencioso.");
                return null;
            }

            CurrentUser = DecodeJwtToUser(tokenToDecode);
            if (CurrentUser == null)
            {
                Log.Warning("El token guardado ha expirado o es inválido. Limpiando...");
                await LogoutAsync(); // Llama a Logout para limpiar el token inválido de donde sea que estuviera.
                return null;
            }

            Log.Information("Inicio de sesión silencioso exitoso para el usuario: {UserName}", CurrentUser.Name);
            _sessionToken = tokenToDecode;
            return CurrentUser;
        }

        /// <summary>
        /// Envía un token de restablecimiento y una nueva contraseña al backend.
        /// </summary>
        /// <param name="token">El token JWT recibido por correo.</param>
        /// <param name="newPassword">La nueva contraseña elegida por el usuario.</param>
        public async Task ResetPasswordAsync(string token, string newPassword)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{AuthServiceBaseUrl}/reset-password", new
                {
                    token,
                    new_password = newPassword
                });

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception(error);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AuthService] Falló el restablecimiento de contraseña.");
                throw; // Propaga la excepción para que el ViewModel la muestre.
            }
        }

        /// <summary>
        /// Decodifica un token JWT para extraer la información del usuario.
        /// NOTA: Esta es una decodificación simple sin validación de firma, ya que confia
        /// en que el token viene del propio backend a través de un canal seguro (localhost).
        /// </summary>
        /// <param name="token">El token JWT en formato string.</param>
        /// <returns>Un objeto User con los datos del token, o null si el token es inválido.</returns>
        private User? DecodeJwtToUser(string token)
        {
            try
            {
                // Un JWT se compone de 3 partes separadas por '.': Cabecera, Payload, Firma.
                var parts = token.Split('.');
                if (parts.Length != 3) return null;

                // La parte del medio (Payload) contiene los datos del usuario.
                string payloadJson = parts[1];
                // El payload está codificado en Base64Url, necesita ser decodificado.
                payloadJson = payloadJson.Replace('-', '+').Replace('_', '/');
                switch (payloadJson.Length % 4)
                {
                    case 2: payloadJson += "=="; break;
                    case 3: payloadJson += "="; break;
                }
                var jsonBytes = Convert.FromBase64String(payloadJson);
                var jsonString = Encoding.UTF8.GetString(jsonBytes);

                // Deserializa el JSON a un objeto anónimo para extraer los claims.
                using var jsonDoc = JsonDocument.Parse(jsonString);
                var root = jsonDoc.RootElement;

                // --- Verificación de la fecha de expiración ---
                if (root.TryGetProperty("exp", out var expClaim) && expClaim.ValueKind == JsonValueKind.Number)
                {
                    var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expClaim.GetInt64()).UtcDateTime;
                    if (expirationTime < DateTime.UtcNow)
                    {
                        Log.Warning("[AuthService] El token de sesión ha expirado. Se requiere nuevo inicio de sesión.");
                        return null; // El token está expirado, trata el login silencioso como fallido.
                    }
                }
                // Crea el objeto User con la información del token.
                return new User
                {
                    Id = root.TryGetProperty("sub", out var sub) ? sub.GetString() ?? "" : "",
                    Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    Email = root.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "",
                    ProfilePictureUrl = root.TryGetProperty("picture", out var pic) ? pic.GetString() ?? "" : ""
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AuthService] Error al decodificar el token JWT.");
                return null;
            }
        }

        /// <summary>
        /// Clase auxiliar para deserializar la respuesta del token desde el backend.
        /// </summary>
        internal class AuthTokenResponse
        {
            public string? Token { get; set; }
        }
        
        /// <summary>
        /// Obtiene la ruta completa al archivo donde se guardará el token de sesión cifrado.
        /// </summary>
        private string GetTokenFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dirPath = Path.Combine(appDataPath, "Ada");
            Directory.CreateDirectory(dirPath);
            return Path.Combine(dirPath, "session.token");
        }
    }
}