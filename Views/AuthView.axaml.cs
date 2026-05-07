using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AsistenteVirtual.Views
{
    /// <summary>
    /// Lógica de interacción para el control de usuario <see cref="AuthView"/>.
    /// </summary>
    /// <remarks>
    /// Este control actúa como un contenedor para los diferentes estados de autenticación
    /// (Inicio de sesión, Registro y Recuperación de cuenta).
    /// </remarks>
    public partial class AuthView : UserControl
    {
        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="AuthView"/>.
        /// </summary>
        public AuthView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}