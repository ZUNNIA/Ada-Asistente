using System;
using System.Windows.Input;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// ViewModel para una notificación de error o advertencia que se muestra en la UI.
    /// Contiene propiedades para el mensaje, severidad y acciones reintentables.
    /// </summary>
    public partial class ErrorNotificationViewModel : ViewModelBase
    {
        // --- Campos Privados ---
        private string _message = string.Empty;
        private bool _isCritical;
        private bool _isRetriable;
        private bool _showProgressBar;
        private ICommand? _retryCommand;

        // --- Propiedades Públicas ---

        /// <summary>
        /// Identificador único para esta instancia de notificación, para poder gestionarla en una colección.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Obtiene o establece el mensaje de la notificación.
        /// </summary>
        public string Message { get => _message; set => SetProperty(ref _message, value); }

        /// <summary>
        /// Obtiene o establece si la notificación es de naturaleza crítica (error) o no (advertencia).
        /// Afecta al estilo visual de la notificación en la UI.
        /// </summary>
        public bool IsCritical { get => _isCritical; set => SetProperty(ref _isCritical, value); }

        /// <summary>
        /// Obtiene o establece si la notificación presenta al usuario una acción que se puede reintentar.
        /// </summary>
        public bool IsRetriable { get => _isRetriable; set => SetProperty(ref _isRetriable, value); }

        /// <summary>
        /// Obtiene o establece si se debe mostrar una barra de progreso, útil durante un reintento de conexión.
        /// </summary>
        public bool ShowProgressBar { get => _showProgressBar; set => SetProperty(ref _showProgressBar, value); }

        /// <summary>
        /// Obtiene o establece el comando que se ejecutará cuando el usuario solicite un reintento.
        /// </summary>
        public ICommand? RetryCommand
        {
            get => _retryCommand;
            set => SetProperty(ref _retryCommand, value);
        }
    }
}
