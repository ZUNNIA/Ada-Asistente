using System.ComponentModel;
using Avalonia.Media.Imaging;

namespace AsistenteVirtual.Services.Interfaces
{
    /// <summary>
    /// Define el contrato para un servicio que gestiona el estado de la interfaz de usuario.
    /// Controla la visibilidad de ventanas, flyouts y otros elementos visuales.
    /// </summary>
    public interface IUIService : INotifyPropertyChanged
    {
        /// <summary>
        /// Obtiene o establece si el panel del historial de conversaciones está visible.
        /// </summary>
        bool IsHistoryPanelOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si el panel de archivos adjuntos está visible.
        /// </summary>
        bool IsFilesPanelOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si el menú flotante de configuración está visible.
        /// </summary>
        bool IsSettingsFlyoutOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si la ventana principal de configuración está visible.
        /// </summary>
        bool IsSettingsWindowOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si la ventana de suscripciones está visible.
        /// </summary>
        bool IsSubscriptionsWindowOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si la ventana de gestión de memorias está visible.
        /// </summary>
        bool IsMemoriesWindowOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si la ventana de información legal está visible.
        /// </summary>
        bool IsLegalWindowOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si la ventana del historial de actualizaciones está visible.
        /// </summary>
        bool IsUpdateHistoryWindowOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si la vista para solicitar el reseteo de contraseña está visible.
        /// </summary>
        bool IsForgotPasswordViewOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si la vista para introducir el token y la nueva contraseña está visible.
        /// </summary>
        bool IsResetPasswordViewOpen { get; set; }

        /// <summary>
        /// Obtiene o establece si la visualización de imagen en pantalla completa está activa.
        /// </summary>
        bool IsImageFullscreenOpen { get; set; }

        /// <summary>
        /// Obtiene o establece la URL o ruta de la imagen a mostrar en pantalla completa.
        /// </summary>
        string? FullscreenImageUrl { get; set; }

        /// <summary>
        /// Obtiene o establece el recurso de imagen (Bitmap) listo para ser renderizado en el overlay de pantalla completa.
        /// </summary>
        Bitmap? FullscreenImage { get; set; }
    }
}