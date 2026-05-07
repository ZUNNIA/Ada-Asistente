using AsistenteVirtual.Services.Interfaces;
using AsistenteVirtual.ViewModels;
using System;

namespace AsistenteVirtual.Services.Implementations
{
    /// <summary>
    /// Servicio centralizado para la propagación de mensajes informativos, advertencias y errores hacia la interfaz de usuario.
    /// </summary>
    /// <remarks>
    /// Implementa un patrón de Mensajería/Eventos desacoplado. Los ViewModels disparan las notificaciones 
    /// sin conocer qué control visual las renderizará, permitiendo una arquitectura más limpia y testeable.
    /// </remarks>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// Evento que se dispara para mostrar notificaciones simples que desaparecen tras un tiempo.
        /// </summary>
        /// <remarks>
        /// El primer parámetro (<see cref="string"/>) es el cuerpo del mensaje.
        /// El segundo parámetro (<see cref="bool"/>) indica si el estilo debe ser de error (true) o informativo (false).
        /// </remarks>
        public event Action<string, bool>? OnShowTemporaryNotification;

        /// <summary>
        /// Evento que se dispara para mostrar diálogos de error complejos que requieren interacción del usuario.
        /// </summary>
        /// <remarks>
        /// Recibe un <see cref="ErrorNotificationViewModel"/> que encapsula el mensaje, detalles técnicos y comandos de acción.
        /// </remarks>
        public event Action<ErrorNotificationViewModel>? OnShowComplexNotification;

        /// <summary>
        /// Solicita la visualización de una notificación efímera en la pantalla principal.
        /// </summary>
        /// <param name="message">El texto descriptivo que se mostrará al usuario.</param>
        /// <param name="isError">Determina si la notificación debe usar el esquema de colores de error crítico.</param>
        public void ShowTemporaryNotification(string message, bool isError)
        {
            OnShowTemporaryNotification?.Invoke(message, isError);
        }

        /// <summary>
        /// Solicita la apertura de un overlay de error con funcionalidades extendidas como copia de trazas o reintentos.
        /// </summary>
        /// <param name="notification">Instancia del ViewModel que contiene la lógica y datos de la notificación compleja.</param>
        /// <exception cref="ArgumentNullException">Se lanza si el parámetro notification es nulo.</exception>
        public void ShowComplexNotification(ErrorNotificationViewModel notification)
        {
            ArgumentNullException.ThrowIfNull(notification);
            OnShowComplexNotification?.Invoke(notification);
        }
    }
}