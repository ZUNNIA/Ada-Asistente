using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Serilog;

namespace AsistenteVirtual.Commands
{
    /// <summary>
    /// Una implementación de ICommand para Avalonia que encapsula acciones como delegados.
    /// </summary>
    /// <remarks>
    /// Inicializa una nueva instancia de la clase RelayCommand.
    /// </remarks>
    /// <param name="execute">La lógica de ejecución asíncrona del comando.</param>
    /// <param name="canExecute">La lógica que determina si el comando puede ejecutarse.</param>
    public class RelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null) : ICommand
    {
        private readonly Func<object?, Task> _executeAsync = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Predicate<object?>? _canExecute = canExecute;

        /// <summary>
        /// Evento que se dispara cuando cambia la condición que determina si el comando puede ejecutarse.
        /// En Avalonia, este evento se invoca manualmente llamando a RaiseCanExecuteChanged().
        /// </summary>
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// Determina si el comando puede ejecutarse en su estado actual.
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// Ejecuta la lógica del comando de forma asíncrona.
        /// </summary>
        public async void Execute(object? parameter)
        {
            try
            {
                await _executeAsync(parameter);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[RelayCommand] Excepción no controlada durante la ejecución para el parámetro: {Parameter}", parameter ?? "null");
            }
        }

        /// <summary>
        /// Ejecuta la lógica del comando de forma asíncrona y devuelve la Tarea para poder esperarla (await).
        /// Este es el método que se usa internamente para encadenar comandos.
        /// </summary>
        public async Task ExecuteAsync(object? parameter)
        {
            await _executeAsync(parameter);
        }

        /// <summary>
        /// Notifica a la UI que la condición de CanExecute ha cambiado y debe ser reevaluada.
        /// Esto permite habilitar o deshabilitar dinámicamente los controles enlazados al comando.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            // Dispara el evento en el hilo principal de la UI para asegurar la compatibilidad.
            Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }
    }
}