using AsistenteVirtual.Services.Interfaces;
using AsistenteVirtual.ViewModels;
using Avalonia.Media.Imaging;

namespace AsistenteVirtual.Services.Implementations
{
    /// <summary>
    /// Fuente única de verdad para el estado de visibilidad y navegación de la interfaz de usuario.
    /// </summary>
    /// <remarks>
    /// Esta clase gestiona el estado global de ventanas modales, paneles laterales y overlays de imagen.
    /// Al heredar de <see cref="ViewModelBase"/>, permite que múltiples componentes de la UI se vinculen (Binding)
    /// a estos estados y reaccionen automáticamente ante cambios.
    /// </remarks>
    public class UIService : ViewModelBase, IUIService
    {
        private bool _isHistoryPanelOpen;
        private bool _isFilesPanelOpen;
        private bool _isSettingsFlyoutOpen;
        private bool _isSettingsWindowOpen;
        private bool _isSubscriptionsWindowOpen;
        private bool _isMemoriesWindowOpen;
        private bool _isLegalWindowOpen;
        private bool _isUpdateHistoryWindowOpen;
        private bool _isForgotPasswordViewOpen;
        private bool _isResetPasswordViewOpen;
        private bool _isImageFullscreenOpen;
        private string? _fullscreenImageUrl;
        private Bitmap? _fullscreenImage;

        /// Obtiene o establece si el panel lateral del historial de chats está expandido.
        public bool IsHistoryPanelOpen { get => _isHistoryPanelOpen; set => SetProperty(ref _isHistoryPanelOpen, value); }

        /// Obtiene o establece si el panel de archivos adjuntos está visible.
        public bool IsFilesPanelOpen { get => _isFilesPanelOpen; set => SetProperty(ref _isFilesPanelOpen, value); }

        /// Obtiene o establece si el menú rápido de configuración (Flyout) está desplegado.
        public bool IsSettingsFlyoutOpen { get => _isSettingsFlyoutOpen; set => SetProperty(ref _isSettingsFlyoutOpen, value); }

        /// Obtiene o establece la visibilidad de la ventana principal de configuración.
        public bool IsSettingsWindowOpen { get => _isSettingsWindowOpen; set => SetProperty(ref _isSettingsWindowOpen, value); }

        /// Obtiene o establece si se muestra la tienda de suscripciones y créditos.
        public bool IsSubscriptionsWindowOpen { get => _isSubscriptionsWindowOpen; set => SetProperty(ref _isSubscriptionsWindowOpen, value); }

        /// Obtiene o establece la visibilidad del gestor de memorias personalizadas.
        public bool IsMemoriesWindowOpen { get => _isMemoriesWindowOpen; set => SetProperty(ref _isMemoriesWindowOpen, value); }

        /// Obtiene o establece si la ventana de términos legales y privacidad está abierta.
        public bool IsLegalWindowOpen { get => _isLegalWindowOpen; set => SetProperty(ref _isLegalWindowOpen, value); }

        /// Obtiene o establece la visibilidad del historial de versiones (Changelog).
        public bool IsUpdateHistoryWindowOpen { get => _isUpdateHistoryWindowOpen; set => SetProperty(ref _isUpdateHistoryWindowOpen, value); }

        /// Obtiene o establece si se muestra el formulario de solicitud de recuperación de cuenta.
        public bool IsForgotPasswordViewOpen { get => _isForgotPasswordViewOpen; set => SetProperty(ref _isForgotPasswordViewOpen, value); }

        /// Obtiene o establece si el formulario de cambio de contraseña mediante token está activo.
        public bool IsResetPasswordViewOpen { get => _isResetPasswordViewOpen; set => SetProperty(ref _isResetPasswordViewOpen, value); }

        /// Obtiene o establece si el visor de imágenes a pantalla completa está activo.
        public bool IsImageFullscreenOpen { get => _isImageFullscreenOpen; set => SetProperty(ref _isImageFullscreenOpen, value); }

        /// Obtiene o establece la URL de la imagen que se está visualizando en pantalla completa.
        public string? FullscreenImageUrl { get => _fullscreenImageUrl; set => SetProperty(ref _fullscreenImageUrl, value); }

        /// Obtiene o establece el recurso de mapa de bits cargado para el visor fullscreen.
        public Bitmap? FullscreenImage { get => _fullscreenImage; set => SetProperty(ref _fullscreenImage, value); }
    }
}