using AsistenteVirtual.Commands;
using AsistenteVirtual.Models;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// ViewModel para la gestión individual de una memoria personalizada del usuario.
    /// </summary>
    /// <remarks>
    /// Maneja el estado de edición local ("In-place editing") y delega las operaciones de persistencia al ViewModel principal.
    /// </remarks>
    public partial class MemoryViewModel : ViewModelBase
    {
        private readonly Memory _memory;
        private bool _isEditing;
        private string _originalContent = string.Empty;

        /// Obtiene el identificador de la memoria asignado por la base de datos.
        public string Id => _memory.Id;

        /// <summary>
        /// Obtiene o establece el contenido textual de la memoria.
        /// </summary>
        public string Content
        {
            get => _memory.Content;
            set
            {
                if (_memory.Content != value)
                {
                    _memory.Content = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Define si el elemento se encuentra actualmente en modo de edición en la interfaz.
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    if (value) { _originalContent = Content; }
                    OnPropertyChanged(nameof(IsDisplaying));
                }
            }
        }

        /// Propiedad de conveniencia para ocultar elementos cuando se está editando.
        public bool IsDisplaying => !IsEditing;

        /// Comando para habilitar el modo edición.
        public ICommand EditCommand { get; }

        /// Comando para confirmar los cambios y persistirlos en el servidor.
        public ICommand ProcessEditCommand { get; }

        /// Comando para solicitar la eliminación permanente de la memoria.
        public ICommand DeleteCommand { get; }

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="MemoryViewModel"/>.
        /// </summary>
        /// <param name="memory">Modelo de datos de la memoria.</param>
        /// <param name="saveAction">Delegado asíncrono para ejecutar el guardado en el backend.</param>
        /// <param name="deleteAction">Delegado asíncrono para ejecutar la eliminación en el backend.</param>
        public MemoryViewModel(Memory memory, Func<MemoryViewModel, Task> saveAction, Func<MemoryViewModel, Task> deleteAction)
        {
            _memory = memory;

            EditCommand = new RelayCommand(_ => { IsEditing = true; return Task.CompletedTask; });

            ProcessEditCommand = new RelayCommand(async _ =>
            {
                if (string.IsNullOrWhiteSpace(Content)) { Content = _originalContent; }
                IsEditing = false;
                if (Content != _originalContent) { await saveAction(this); }
            });

            DeleteCommand = new RelayCommand(async _ => await deleteAction(this));
        }
    }
}