using Avalonia.Controls;
using System;
using System.Windows.Input;
using AsistenteVirtual.Commands;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// ViewModel que representa una notificación visual en el sistema, ya sea un error crítico o una advertencia.
    /// </summary>
    /// <remarks>
    /// Encapsula la lógica de visualización, comandos de cierre y la capacidad de copiar trazas de error al portapapeles.
    /// </remarks>
    public partial class ErrorNotificationViewModel : ViewModelBase
    {
        private string _message = string.Empty;
        private bool _isCritical;
        private string _detailsToCopy = string.Empty;
        private ICommand? _primaryActionCommand;
        private string _primaryActionText = string.Empty;

        /// Obtiene el identificador único universal para esta instancia de notificación.
        public Guid Id { get; } = Guid.NewGuid();

        /// Obtiene o establece el mensaje descriptivo que verá el usuario.
        public string Message { get => _message; set => SetProperty(ref _message, value); }

        /// Obtiene o establece si la notificación representa un fallo crítico que requiere atención inmediata.
        public bool IsCritical { get => _isCritical; set => SetProperty(ref _isCritical, value); }

        /// Obtiene o establece la información técnica (ej. StackTrace) que puede ser copiada por el usuario.
        public string DetailsToCopy { get => _detailsToCopy; set => SetProperty(ref _detailsToCopy, value); }

        /// Obtiene o establece el comando de acción principal asociado a la notificación.
        public ICommand? PrimaryActionCommand { get => _primaryActionCommand; set => SetProperty(ref _primaryActionCommand, value); }

        /// Obtiene o establece el texto del botón para la acción principal.
        public string PrimaryActionText { get => _primaryActionText; set => SetProperty(ref _primaryActionText, value); }

        /// Indica si la notificación tiene detalles técnicos que permiten habilitar el botón de copiado.
        public bool CanCopyDetails => IsCritical && !string.IsNullOrWhiteSpace(DetailsToCopy);

        /// Comando para eliminar la notificación de la colección activa en la UI.
        public ICommand? CloseCommand { get; set; }

        /// Comando interno para gestionar la copia de detalles al portapapeles del sistema.
        public ICommand CopyDetailsCommand { get; }

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="ErrorNotificationViewModel"/> y configura el comando de portapapeles.
        /// </summary>
        public ErrorNotificationViewModel()
        {
            CopyDetailsCommand = new RelayCommand(async _ =>
            {
                if (TopLevel.GetTopLevel(null)?.Clipboard is { } clipboard && !string.IsNullOrWhiteSpace(DetailsToCopy))
                {
                    await clipboard.SetTextAsync(DetailsToCopy);
                }
            });
        }
    }
}