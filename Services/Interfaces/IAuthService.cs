using AsistenteVirtual.Models;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services.Interfaces
{
    /// <summary>
    /// Define el contrato para la gestión de identidad, seguridad y ciclo de vida de la sesión del usuario.
    /// </summary>
    /// <remarks>
    /// Las implementaciones deben asegurar el manejo cifrado de tokens JWT y orquestar los flujos 
    /// de OAuth2 Loopback necesarios para aplicaciones de escritorio.
    /// </remarks>
    public interface IAuthService
    {
        /// <summary>
        /// Obtiene el perfil del usuario autenticado actualmente en la sesión.
        /// </summary>
        /// <value>Instancia de <see cref="User"/> o null si no hay una sesión activa.</value>
        User? CurrentUser { get; }

        /// <summary>
        /// Inicia un proceso de inicio de sesión interactivo abriendo el navegador del sistema.
        /// </summary>
        /// <returns>Tarea que resulta en el objeto <see cref="User"/> validado o null si el proceso fue cancelado.</returns>
        Task<User?> LoginAsync();

        /// <summary>
        /// Crea una nueva cuenta de usuario en el sistema mediante credenciales tradicionales.
        /// </summary>
        /// <param name="username">Nombre de usuario público deseado.</param>
        /// <param name="email">Correo electrónico único para la cuenta.</param>
        /// <param name="password">Contraseña en texto plano (será hasheada antes de viajar al servidor).</param>
        /// <returns>Tarea que resulta en el objeto <see cref="User"/> recién creado.</returns>
        Task<User?> RegisterWithEmailAsync(string username, string email, string password);

        /// <summary>
        /// Autentica al usuario utilizando credenciales de correo y contraseña.
        /// </summary>
        /// <param name="identifier">Nombre de usuario o dirección de correo electrónico.</param>
        /// <param name="password">Contraseña de acceso.</param>
        /// <returns>Tarea que resulta en el usuario autenticado o null si las credenciales son erróneas.</returns>
        Task<User?> LoginWithEmailAsync(string identifier, string password);

        /// <summary>
        /// Solicita al servidor el envío de un correo electrónico para la recuperación de la contraseña.
        /// </summary>
        /// <param name="email">El correo electrónico asociado a la cuenta que se desea recuperar.</param>
        /// <returns>Tarea que representa la finalización de la petición de envío.</returns>
        Task RequestPasswordResetAsync(string email);

        /// <summary>
        /// Intenta recuperar una sesión guardada localmente de forma segura sin intervención del usuario.
        /// </summary>
        /// <returns>Tarea que resulta en el usuario si el token es válido; de lo contrario, null.</returns>
        Task<User?> SilentLoginAsync();

        /// <summary>
        /// Finaliza la sesión actual y purga cualquier credencial sensible de la memoria y el almacenamiento persistente.
        /// </summary>
        /// <returns>Tarea que representa la operación de cierre de sesión.</returns>
        Task LogoutAsync();

        /// <summary>
        /// Recupera el token JWT de la sesión vigente para autorizar peticiones HTTP Bearer.
        /// </summary>
        /// <returns>Tarea que resulta en el string del token o null si no existe sesión activa.</returns>
        Task<string?> GetCurrentUserTokenAsync();

        /// <summary>
        /// Actualiza la contraseña de una cuenta utilizando un token de seguridad recibido por correo.
        /// </summary>
        /// <param name="token">Token JWT de un solo uso para el reseteo de contraseña.</param>
        /// <param name="newPassword">La nueva contraseña elegida por el usuario.</param>
        /// <returns>Tarea que representa la operación de actualización.</returns>
        Task ResetPasswordAsync(string token, string newPassword);
    }
}