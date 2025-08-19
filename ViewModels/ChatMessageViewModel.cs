using AsistenteVirtual.Models;
using Avalonia.Media;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// ViewModel que representa un único mensaje en la vista del chat.
    /// Envuelve el modelo <see cref="ChatMessage"/> y añade propiedades específicas 
    /// de la UI, como los colores, para el data binding.
    /// </summary>
    public partial class ChatMessageViewModel : ViewModelBase
    {
        private readonly ChatMessage _message;
        private IBrush _roleColor;
        private IBrush _contentColor = Brushes.White;

        /// <summary>
        /// Obtiene el ID del mensaje asignado por OpenAI.
        /// Es nulo para mensajes optimistas que aún no se han confirmado por el servidor.
        /// </summary>
        public string? MessageId => _message.MessageId;

        /// <summary>
        /// Obtiene el rol del emisor del mensaje (ej. "Usuario", "Asistente").
        /// </summary>
        public string Role => _message.Role;

        /// <summary>
        /// Obtiene o establece el contenido textual del mensaje.
        /// Notifica a la UI cuando su valor cambia.
        /// </summary>
        public string Content
        {
            get => _message.Content;
            set
            {
                if (_message.Content != value)
                {
                    _message.Content = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Obtiene o establece un valor que indica si la respuesta del asistente fue interrumpida.
        /// </summary>
        public bool IsInterrupted
        {
            get => _message.IsInterrupted;
            set
            {
                if (_message.IsInterrupted != value)
                {
                    _message.IsInterrupted = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Obtiene o establece el color del texto del rol para la UI.
        /// </summary>
        public IBrush RoleColor
        {
            get => _roleColor;
            set => SetProperty(ref _roleColor, value);
        }

        /// <summary>
        /// Obtiene o establece el color del contenido del mensaje para la UI.
        /// </summary>
        public IBrush ContentColor
        {
            get => _contentColor;
            set => SetProperty(ref _contentColor, value);
        }

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="ChatMessageViewModel"/>.
        /// </summary>
        /// <param name="message">El modelo de datos <see cref="ChatMessage"/> a envolver.</param>
        public ChatMessageViewModel(ChatMessage message)
        {
            _message = message;
            _roleColor = new SolidColorBrush(GetRoleColorBasedOnRole(message.Role));
        }

        /// <summary>
        /// Determina el color para un rol específico.
        /// </summary>
        /// <param name="role">El rol del mensaje.</param>
        /// <returns>Un objeto Color que representa el color.</returns>
        private static Color GetRoleColorBasedOnRole(string role) => role.ToLowerInvariant() switch
        {
            "usuario" => Colors.LightSkyBlue,
            "asistente" => Colors.LightGreen,
            "sistema" => Colors.Orange,
            "web" => Colors.MediumPurple,
            _ => Colors.Gray,
        };
    }
}
