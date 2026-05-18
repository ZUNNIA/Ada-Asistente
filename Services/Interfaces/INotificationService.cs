using AsistenteVirtual.ViewModels;
using System;

namespace AsistenteVirtual.Services.Interfaces
{
    /// <summary>
    /// Define el contrato para el sistema de notificaciones visuales de la aplicación.
    /// </summary>
    /// <remarks>
    /// Implementa un patrón de mensajería desacoplado para disparar alertas desde 
    /// cualquier capa sin depender de controles de UI específicos.
    /// </remarks>
    public interface INotificationService
    {
        /// Evento disparado para mostrar notificaciones simples de corta duración.
        event Action<string, bool> OnShowTemporaryNotification;

        /// Evento disparado para desplegar diálogos de error complejos con acciones extendidas.
        event Action<ErrorNotificationViewModel> OnShowComplexNotification;

        /// <summary>
        /// Solicita la visualización de una notificación informativa o de error efímera.
        /// </summary>
        /// <param name="message">Cuerpo del mensaje a mostrar.</param>
        /// <param name="isError">Define si la notificación debe usar el esquema visual de error crítico.</param>
        void ShowTemporaryNotification(string message, bool isError);

        /// <summary>
        /// Solicita el despliegue de un overlay de error persistente que requiere interacción.
        /// </summary>
        /// <param name="notification">Instancia del ViewModel configurado con los datos del error.</param>
        void ShowComplexNotification(ErrorNotificationViewModel notification);
    }
}