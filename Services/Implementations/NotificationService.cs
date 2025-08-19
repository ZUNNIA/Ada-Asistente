using System;

namespace AsistenteVirtual.Services
{
    /// <summary>
    /// Implementación de INotificationService. Actúa como un simple bus de eventos
    /// para desacoplar la solicitud de notificaciones (desde los ViewModels o otros servicios)
    /// de su presentación real en la Vista.
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Evento que se dispara cuando se debe mostrar una notificación. 
        /// El primer parámetro es el mensaje y el segundo indica si es un error.
        /// </summary>
        public event Action<string, bool>? OnShowTemporaryNotification;

        /// <summary>
        /// Invoca el evento para solicitar que se muestre una notificación en la UI.
        /// </summary>
        /// <param name="message">El texto del mensaje a mostrar.</param>
        /// <param name="isError">True si es una notificación de error; false para advertencia o información.</param>
        public void ShowTemporaryNotification(string message, bool isError)
        {
            // Dispara el evento para que cualquier suscriptor (como el MainViewModel) pueda reaccionar.
            OnShowTemporaryNotification?.Invoke(message, isError);
        }
    }
}
