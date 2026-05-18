using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AsistenteVirtual.ViewModels;
using Avalonia.Threading;

namespace AsistenteVirtual.Views
{
    /// <summary>
    /// Representa el área central de interacción del chat, gestionando eventos de entrada avanzados.
    /// </summary>
    /// <remarks>
    /// Implementa comportamientos específicos de la UI como el scroll con el botón central del ratón
    /// y la captura de teclas de acceso rápido (Enter vs Shift+Enter) para mejorar la UX.
    /// </remarks>
    public partial class ChatArea : UserControl
    {
        private Point _middleClickStartPos;
        private bool _isMiddleClickScrolling;

        /// <summary>
        /// Inicializa el control y registra manejadores de eventos de bajo nivel.
        /// </summary>
        public ChatArea()
        {
            InitializeComponent();

            // Gestión de entrada de texto mediante Tunneling para interceptar el Enter antes que el TextBox.
            TextBox? userInputEditor = this.FindControl<TextBox>("UserInputEditor");
            userInputEditor?.AddHandler(KeyDownEvent, UserInputEditor_PreviewKeyDown, RoutingStrategies.Tunnel);

            // Configuración de scroll por arrastre con botón central.
            this.AttachedToVisualTree += (s, e) =>
            {
                ListBox? listBox = this.FindControl<ListBox>("ChatListBox");
                if (listBox != null)
                {
                    // Esperar a que el template se aplique para encontrar el ScrollViewer
                    Dispatcher.UIThread.Post(() =>
                    {
                        ScrollViewer? scrollViewer = listBox.FindControl<ScrollViewer>("PART_ScrollViewer");
                        if (scrollViewer != null)
                        {
                            SetupScrollViewer(scrollViewer);
                        }
                    }, DispatcherPriority.Loaded);
                }
            };
        }

        private void SetupScrollViewer(ScrollViewer scrollViewer)
        {
            scrollViewer.PointerPressed += ScrollViewer_PointerPressed;
            scrollViewer.PointerMoved += ScrollViewer_PointerMoved;
            scrollViewer.PointerReleased += ScrollViewer_PointerReleased;
            
            if (DataContext is MainViewModel vm)
            {
                vm.ChatVM.Messages.CollectionChanged += (sender, args) => ScrollToBottom();
            }
            
            // También escuchar cambios de DataContext
            DataContextChanged += (s, e) =>
            {
                if (DataContext is MainViewModel vm2)
                {
                    vm2.ChatVM.Messages.CollectionChanged += (sender, args) => ScrollToBottom();
                }
            };
        }

        /// <summary>
        /// Captura la posición inicial al presionar el botón central del ratón para iniciar el scroll.
        /// </summary>
        private void ScrollViewer_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                _isMiddleClickScrolling = true;
                _middleClickStartPos = e.GetPosition(this);
                e.Pointer.Capture(sender as Control);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Calcula el desplazamiento del scroll basándose en el movimiento del puntero desde el punto de origen.
        /// </summary>
        private void ScrollViewer_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isMiddleClickScrolling && sender is ScrollViewer sv)
            {
                Point currentPos = e.GetPosition(this);
                double deltaY = currentPos.Y - _middleClickStartPos.Y;
                sv.Offset = new Vector(sv.Offset.X, sv.Offset.Y + (deltaY / 10)); // Factor de suavizado.
            }
        }

        /// <summary>
        /// Finaliza la operación de scroll y libera la captura del puntero.
        /// </summary>
        private void ScrollViewer_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isMiddleClickScrolling)
            {
                _isMiddleClickScrolling = false;
                e.Pointer.Capture(null);
            }
        }

        /// <summary>
        /// Establece el scroll en la parte superior.
        /// </summary>
        private void ScrollToBottom()
        {
            Dispatcher.UIThread.Post(() =>
            {
                ListBox? listBox = this.FindControl<ListBox>("ChatListBox");
                ScrollViewer? scrollViewer = listBox?.FindControl<ScrollViewer>("PART_ScrollViewer");
                scrollViewer?.ScrollToEnd();
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// Intercepta la tecla Enter para enviar el mensaje, permitiendo saltos de línea con Shift+Enter.
        /// </summary>
        private void UserInputEditor_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.KeyModifiers != KeyModifiers.Shift)
            {
                e.Handled = true;
                if (DataContext is MainViewModel vm && vm.ChatVM.SendMessageCommand.CanExecute(null))
                {
                    vm.ChatVM.SendMessageCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Gestiona el envío de ediciones de mensajes individuales.
        /// </summary>
        private void EditMsgTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Si no está presionado Shift, enviamos el mensaje
                if (e.KeyModifiers == KeyModifiers.None)
                {
                    e.Handled = true; // Evita que el TextBox procese el Enter (salto de línea)
                    if (sender is TextBox tb && tb.DataContext is ChatMessageViewModel msgVm)
                    {
                        MainViewModel mainVm = (MainViewModel)DataContext!;
                        if (mainVm.ChatVM.CommitEditCommand.CanExecute(msgVm))
                        {
                            mainVm.ChatVM.CommitEditCommand.Execute(msgVm);
                        }
                    }
                }
                // Si Shift está presionado, e.Handled permanece false y el TextBox hace el salto de línea normal
            }
        }
    }
}