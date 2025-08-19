using System;

namespace AsistenteVirtual.Services
{
    /// <summary>
    /// Define el contrato para un servicio que gestiona notificaciones en la UI.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Evento que se dispara cuando se debe mostrar una notificación.
        /// </summary>
        event Action<string, bool> OnShowTemporaryNotification;

        /// <summary>
        /// Solicita mostrar una notificación temporal.
        /// </summary>
        /// <param name="message">El mensaje a mostrar.</param>
        /// <param name="isError">Indica si es un error (true) o una advertencia/información (false).</param>
        void ShowTemporaryNotification(string message, bool isError);
    }
}
