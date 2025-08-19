using AsistenteVirtual.Models;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services
{
    /// <summary>
    /// Define el contrato para un servicio de autenticación.
    /// Abstrae la lógica de inicio de sesión, cierre de sesión y gestión del estado del usuario.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Obtiene el usuario actualmente autenticado en la aplicación.
        /// Es nulo si no hay una sesión activa.
        /// </summary>
        User? CurrentUser { get; }

        /// <summary>
        /// Inicia el flujo de autenticación interactivo a través del backend.
        /// Este método orquestará la apertura del navegador y la recepción del token.
        /// </summary>
        /// <returns>El objeto User si el inicio de sesión es exitoso; de lo contrario, null.</returns>
        Task<User?> LoginAsync();

        /// <summary>
        /// Registra un nuevo usuario con correo y contraseña.
        /// </summary>
        /// <param name="username">El nombre de usuario elegido.</param>
        /// <param name="email">El correo electrónico del usuario.</param>
        /// <param name="password">La contraseña en texto plano.</param>
        /// <returns>El objeto User si el registro y el inicio de sesión automático son exitosos; de lo contrario, null.</returns>
        Task<User?> RegisterWithEmailAsync(string username, string email, string password);

        /// <summary>
        /// Inicia sesión con un identificador (usuario o correo) y contraseña.
        /// </summary>
        /// <param name="identifier">El nombre de usuario o correo.</param>
        /// <param name="password">La contraseña.</param>
        /// <returns>El objeto User si el inicio de sesión es exitoso; de lo contrario, null.</returns>
        Task<User?> LoginWithEmailAsync(string identifier, string password);

        /// <summary>
        /// Solicita un enlace para restablecer la contraseña para un correo electrónico determinado.
        /// </summary>
        /// <param name="email">El correo electrónico del usuario que solicita el restablecimiento.</param>
        Task RequestPasswordResetAsync(string email);

        /// <summary>
        /// Intenta restaurar una sesión de usuario de forma silenciosa si existe un token guardado.
        /// </summary>
        /// <returns>El objeto User si la restauración es exitosa; de lo contrario, null.</returns>
        Task<User?> SilentLoginAsync();

        /// <summary>
        /// Cierra la sesión del usuario actual, invalidando el token local.
        /// </summary>
        Task LogoutAsync();

        /// <summary>
        /// Obtiene el token de sesión (JWT) del usuario actual para autenticar peticiones al backend.
        /// </summary>
        /// <returns>El token de sesión como un string, o null si el usuario no está autenticado.</returns>
        Task<string?> GetCurrentUserTokenAsync();

        /// <summary>
        /// Envía un token de restablecimiento y una nueva contraseña al backend.
        /// </summary>
        /// <param name="token">El token JWT recibido por correo.</param>
        /// <param name="newPassword">La nueva contraseña elegida por el usuario.</param>
        Task ResetPasswordAsync(string token, string newPassword);
    }
}