using AsistenteVirtual.Commands;
using AsistenteVirtual.Models;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// ViewModel para un ítem individual en la lista del historial de conversaciones.
    /// Gestiona el estado de visualización y las acciones para una conversación específica, incluyendo la edición del título.
    /// </summary>
    public partial class ConversationHistoryViewModel : ViewModelBase
    {
        private readonly Conversation _conversation;
        private bool _isEditingTitle;
        private string _originalTitle = string.Empty;
        private bool _isSelected;

        /// <summary>
        /// Obtiene el ID del hilo de OpenAI asociado a esta conversación.
        /// </summary>
        public string ThreadId => _conversation.ThreadId;

        /// <summary>
        /// Obtiene o establece el título de la conversación, notificando a la UI de los cambios.
        /// </summary>
        public string Title
        {
            get => _conversation.Title;
            set
            {
                if (_conversation.Title != value)
                {
                    _conversation.Title = value;
                    OnPropertyChanged(); // Notificar manualmente.
                }
            }
        }

        /// <summary>
        /// Obtiene la fecha de creación de la conversación.
        /// </summary>
        public DateTime CreatedAt => _conversation.CreatedAt;

        /// <summary>
        /// Obtiene o establece un valor que indica si el título está actualmente en modo de edición.
        /// </summary>
        public bool IsEditingTitle
        {
            get => _isEditingTitle;
            set
            {
                if (SetProperty(ref _isEditingTitle, value))
                {
                    if (value)
                    {
                        OriginalTitle = Title; // Guarda el título actual al empezar a editar
                    }
                    OnPropertyChanged(nameof(IsDisplayingTitle));
                }
            }
        }

        /// <summary>
        /// Obtiene o establece si esta conversación está seleccionada actualmente.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// Propiedad calculada que indica si el título debe mostrarse como texto (no en modo de edición).
        /// </summary>
        public bool IsDisplayingTitle => !IsEditingTitle;

        /// <summary>
        /// Almacena el título original antes de que comience la edición, para poder revertirlo.
        /// </summary>
        public string OriginalTitle
        {
            get => _originalTitle;
            set => SetProperty(ref _originalTitle, value);
        }

        /// <summary>
        /// Comando para iniciar la edición del título.
        /// </summary>
        public ICommand RenameCommand { get; }

        /// <summary>
        /// Comando para iniciar la eliminación de la conversación.
        /// </summary>
        public ICommand DeleteCommand { get; }

        /// <summary>
        /// Comando para cargar esta conversación específica, inyectado desde el MainViewModel.
        /// </summary>
        public ICommand LoadCommand { get; }

        /// <summary>
        /// Comando para procesar la edición del título, inyectado desde el MainViewModel.
        /// </summary>
        public ICommand ProcessTitleEditCommand { get; }

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="ConversationHistoryViewModel"/>.
        /// </summary>
        /// <param name="conversation">El modelo de datos de la conversación a representar.</param>
        /// <param name="loadConversationCommand">El comando del MainViewModel que se ejecutará.</param>
        /// <param name="renameAction">La acción a ejecutar cuando se solicita renombrar.</param>
        /// <param name="deleteAction">La acción asíncrona a ejecutar cuando se solicita eliminar.</param>
        public ConversationHistoryViewModel(
            Conversation conversation,
            ICommand loadConversationCommand,
            ICommand processTitleEditCommand,
            Action<ConversationHistoryViewModel> renameAction,
            Func<ConversationHistoryViewModel, Task> deleteAction)
        {
            _conversation = conversation;
            LoadCommand = new RelayCommand(async _ => await ((RelayCommand)loadConversationCommand).ExecuteAsync(this));
            ProcessTitleEditCommand = new RelayCommand(async _ => await ((RelayCommand)processTitleEditCommand).ExecuteAsync(this));
            RenameCommand = new RelayCommand(_ => { renameAction(this); return Task.CompletedTask; });
            DeleteCommand = new RelayCommand(async _ => await deleteAction(this));
        }
        /// <summary>
        /// Obtiene el modelo de datos subyacente de la conversación.
        /// </summary>
        /// <returns>La instancia del modelo <see cref="Conversation"/>.</returns>
        public Conversation GetModel() => _conversation;
    }
}