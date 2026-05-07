using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;

namespace AsistenteVirtual.Views
{
    /// <summary>
    /// Lógica de la vista de la tienda de productos y suscripciones.
    /// </summary>
    public partial class StoreView : UserControl
    {
        public StoreView()
        {
            InitializeComponent();
        }


        /// <summary>
        /// Captura y detiene la propagación de eventos de puntero para evitar que el clic
        /// en el contenido de la tienda cierre el overlay de fondo.
        /// </summary>
        private void Content_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
        }


        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}