using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace AsistenteVirtual.Views
{
    /// <summary>
    /// Control de usuario que representa el panel lateral izquierdo con el historial.
    /// Contiene la lógica visual para la lista de conversaciones y la edición de títulos.
    /// </summary>
    public partial class HistoryPanel : UserControl
    {
        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="HistoryPanel"/>.
        /// </summary>
        public HistoryPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Carga los componentes XAML.
        /// </summary>
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Maneja el evento KeyDown del editor de títulos.
        /// Evita que el foco se pierda al presionar las flechas de dirección cuando se llega al límite del texto.
        /// </summary>
        /// <param name="sender">El TextBox que origina el evento.</param>
        /// <param name="e">Argumentos del evento de teclado.</param>
        public void TitleEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Left or Key.Right)
            {
                // Marca el evento como manejado para evitar que Avalonia mueva el foco
                // a otro control (navegación direccional) cuando el cursor está en los bordes.
                e.Handled = true;
            }
        }

        /// <summary>
        /// Se ejecuta cuando el TextBox de edición se adjunta al árbol visual (se hace visible).
        /// Fuerza el foco en el control y selecciona todo el texto para facilitar la edición rápida.
        /// </summary>
        /// <param name="sender">El TextBox que aparece.</param>
        /// <param name="e">Argumentos del evento.</param>
        public void TitleEditorAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // Usamos el Dispatcher para asegurar que el control esté completamente renderizado
                // antes de intentar darle el foco.
                Dispatcher.UIThread.Post(() =>
                {
                    _ = tb.Focus();
                    tb.SelectAll();
                });
            }
        }
    }
}