using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AsistenteVirtual.Services;
using AsistenteVirtual.ViewModels;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Media;
using System.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AsistenteVirtual.Views
{
    /// <summary>
    /// La ventana principal de la aplicación.
    /// Su responsabilidad es manejar interacciones puras de la UI y actuar como puente
    /// para servicios que requieren una referencia a la ventana, como el StorageProvider.
    /// </summary>
    public partial class MainWindow : Window, IStorageService
    {
        // --- State Machine para gestionar el estado de los paneles ---
        private enum PanelState { Closed, Opening, Open, Closing }
        private PanelState _leftPanelState = PanelState.Closed;
        private PanelState _rightPanelState = PanelState.Closed;

        // Temporizadores para el "debounce" (retraso) al salir del área
        private readonly DispatcherTimer _leftCloseTimer;
        private readonly DispatcherTimer _rightCloseTimer;

        // Tokens para cancelar animaciones en curso
        private CancellationTokenSource? _leftAnimationCts;
        private CancellationTokenSource? _rightAnimationCts;
        
        public MainWindow()
        {
            InitializeComponent();
            // El DataContext se establecerá desde App.axaml.cs a través de la inyección de dependencias.
            this.Opened += async (sender, args) =>
            {
                if (this.DataContext is MainViewModel vm)
                {
                    await vm.InitializeAsync();
                }
            };
            // Asigna la transformación inicial
            HistoryPanelBorder.RenderTransform = new TranslateTransform(-250, 0);
            FilesPanelBorder.RenderTransform = new TranslateTransform(250, 0);

            // --- Inicializa los DispatcherTimers ---
            _leftCloseTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, OnLeftCloseTimerTick);
            _leftCloseTimer.IsEnabled = false;
            _rightCloseTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, OnRightCloseTimerTick);
            _rightCloseTimer.IsEnabled = false;

            // Suscribirse a eventos de la ventana
            this.KeyDown += MainWindow_KeyDown;
            Closing += MainWindow_Closing;
            this.PointerExited += MainWindow_PointerExited;
            AddHandler(DragDrop.DragEnterEvent, DragEnter);
            AddHandler(DragDrop.DragLeaveEvent, DragLeave);
            AddHandler(DragDrop.DropEvent, Drop);
        }

        #region Animaciones de Paneles Laterales

        /// <summary>
        /// El director de orquesta de las animaciones. Este método construye y ejecuta
        /// una animación de deslizamiento para cualquier control que se le pase.
        /// Es el corazón de la fluidez de los paneles.
        /// </summary>
        /// <param name="control">El control visual (el panel) que se va a animar.</param>
        /// <param name="targetX">La coordenada X final a la que el panel debe deslizarse.</param>
        /// <param name="token">Un token de cancelación para detener la animación si se inicia una nueva.</param>
        private async Task AnimatePanelAsync(Control control, double targetX, CancellationToken token)
        {
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = new CubicEaseOut(),
                // EL TRUCAZO:
                // FillMode.Forward le dice a la animación que el estado final del control
                // debe persistir después de que la animación termine. Esto evita que el panel
                // "salte" de vuelta a su posición original al finalizar.
                FillMode = FillMode.Forward
            };
            animation.Children.Add(new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, targetX) } });

            try
            {
                await animation.RunAsync(control, token);
                // ¡VICTORIA!
                // Como doble seguro, después de que la animación termine (si no fue cancelada),
                // fuerza explícitamente el valor final en la propiedad. Esto elimina cualquier
                // posible desincronización entre el motor de renderizado y el estado lógico del control.
                if (!token.IsCancellationRequested && control.RenderTransform is TranslateTransform tt)
                {
                    tt.X = targetX;
                }
            }
            catch (OperationCanceledException) 
            { 
                // Esto es normal. Si el usuario mueve el ratón rápidamente, cancela
                // la animación anterior para empezar la nueva. Silencia la excepción.
            }
        }

        // --- LÓGICA PARA EL PANEL IZQUIERDO ---

        /// <summary>
        /// Se activa cuando el ratón entra en el área de activación del panel izquierdo.
        /// Su misión es iniciar el proceso para mostrar el panel.
        /// </summary>
        private void HistoryTriggerArea_PointerEntered(object? sender, PointerEventArgs e)
        {
            _leftCloseTimer.IsEnabled = false; // Detiene cualquier cierre pendiente.
            if (_leftPanelState == PanelState.Closed || _leftPanelState == PanelState.Closing)
            {
                _leftAnimationCts?.Cancel();
                _leftAnimationCts = new CancellationTokenSource();
                _leftPanelState = PanelState.Opening;
                _ = AnimatePanelAsync(HistoryPanelBorder, 0, _leftAnimationCts.Token)
                    .ContinueWith(t => { if (t.IsCompletedSuccessfully) _leftPanelState = PanelState.Open; });
            }
        }

        /// <summary>
        /// Se activa cuando el ratón entra en el propio panel izquierdo (que ya está visible).
        /// Su única función es asegurarse de que el temporizador de cierre se detenga,
        /// manteniendo el panel abierto mientras el cursor esté sobre él.
        /// </summary>
        private void HistoryPanel_PointerEntered(object? sender, PointerEventArgs e)
        {
            _leftCloseTimer.IsEnabled = false;
        }

        /// <summary>
        /// Se activa cuando el ratón sale del área de activación.
        /// Inicia el temporizador que, tras un breve retraso, ocultará el panel.
        /// </summary>
        private void HistoryTriggerArea_PointerExited(object? sender, PointerEventArgs e)
        {
            _leftCloseTimer.IsEnabled = true;
        }

        /// <summary>
        /// Se activa cuando el ratón sale del panel principal.
        /// También inicia el temporizador para ocultar el panel.
        /// </summary>
        private void HistoryPanel_PointerExited(object? sender, PointerEventArgs e)
        {
            _leftCloseTimer.IsEnabled = true; 
        }

        /// <summary>
        /// Se activa cuando el temporizador de cierre del panel izquierdo completa su cuenta atrás.
        /// Confirma que el panel debe cerrarse y lanza la animación para ocultarlo.
        /// </summary>
        private void OnLeftCloseTimerTick(object? sender, EventArgs e)
        {
            _leftCloseTimer.IsEnabled = false;
            if (_leftPanelState == PanelState.Open || _leftPanelState == PanelState.Opening)
            {
                _leftAnimationCts?.Cancel();
                _leftAnimationCts = new CancellationTokenSource();
                _leftPanelState = PanelState.Closing;
                _ = AnimatePanelAsync(HistoryPanelBorder, -250, _leftAnimationCts.Token)
                    .ContinueWith(t => { if (t.IsCompletedSuccessfully) _leftPanelState = PanelState.Closed; });
            }
        }

        // --- LÓGICA PARA EL PANEL DERECHO ---
        
        private void FilesTriggerArea_PointerEntered(object? sender, PointerEventArgs e)
        {
            _rightCloseTimer.IsEnabled = false;
            if (_rightPanelState == PanelState.Closed || _rightPanelState == PanelState.Closing)
            {
                _rightAnimationCts?.Cancel();
                _rightAnimationCts = new CancellationTokenSource();
                _rightPanelState = PanelState.Opening;
                _ = AnimatePanelAsync(FilesPanelBorder, 0, _rightAnimationCts.Token)
                    .ContinueWith(t => { if (t.IsCompletedSuccessfully) _rightPanelState = PanelState.Open; });
            }
        }

        private void FilesPanel_PointerEntered(object? sender, PointerEventArgs e)
        {
            _rightCloseTimer.IsEnabled = false;
        }

        private void FilesTriggerArea_PointerExited(object? sender, PointerEventArgs e)
        {
            _rightCloseTimer.IsEnabled = true;
        }

        private void FilesPanel_PointerExited(object? sender, PointerEventArgs e)
        {
            _rightCloseTimer.IsEnabled = true;
        }

        private void OnRightCloseTimerTick(object? sender, EventArgs e)
        {
            _rightCloseTimer.IsEnabled = false;
            if (_rightPanelState == PanelState.Open || _rightPanelState == PanelState.Opening)
            {
                _rightAnimationCts?.Cancel();
                _rightAnimationCts = new CancellationTokenSource();
                _rightPanelState = PanelState.Closing;
                _ = AnimatePanelAsync(FilesPanelBorder, 250, _rightAnimationCts.Token)
                    .ContinueWith(t => { if (t.IsCompletedSuccessfully) _rightPanelState = PanelState.Closed; });
            }
        }

        // --- LÓGICA GENERAL ---

        /// <summary>
        /// Se activa cuando el puntero del ratón sale por completo de la ventana de la aplicación.
        /// Es la última línea de defensa para asegurarse de que ambos paneles se oculten.
        /// </summary>
        private async void MainWindow_PointerExited(object? sender, PointerEventArgs e)
        {
            // Si el panel izquierdo está abierto o abriéndose, lo cierra.
            if (_leftPanelState == PanelState.Open || _leftPanelState == PanelState.Opening)
            {
                _leftAnimationCts?.Cancel();
                _leftAnimationCts = new CancellationTokenSource();
                _leftPanelState = PanelState.Closing;
                await AnimatePanelAsync(this.HistoryPanelBorder, -250, _leftAnimationCts.Token);
                _leftPanelState = PanelState.Closed;
            }

            // Hace lo mismo con el panel derecho.
            if (_rightPanelState == PanelState.Open || _rightPanelState == PanelState.Opening)
            {
                _rightAnimationCts?.Cancel();
                _rightAnimationCts = new CancellationTokenSource();
                _rightPanelState = PanelState.Closing;
                await AnimatePanelAsync(this.FilesPanelBorder, 250, _rightAnimationCts.Token);
                _rightPanelState = PanelState.Closed;
            }
        }

        #endregion

        /// <summary>
        /// Maneja el evento de pulsación de teclas en el editor de entrada principal.
        /// Su propósito es capturar la tecla 'Enter' para enviar el mensaje.
        /// </summary>
        private void UserInputEditor_KeyDown(object? sender, KeyEventArgs e)
        {
            // EL TRUCAZO:
            // Es importante comprobar si hay modificadores de teclado (como Shift).
            // Queremos que 'Enter' envíe el mensaje, pero que 'Shift+Enter' siga
            // permitiendo al usuario escribir un salto de línea, una funcionalidad
            // estándar en la mayoría de aplicaciones de chat. Por eso la condición
            // e.KeyModifiers != KeyModifiers.Shift es tan importante.
            if (e.Key == Key.Enter && e.KeyModifiers != KeyModifiers.Shift)
            {
                // ¡VICTORIA!
                // Al marcar el evento como "manejado" (e.Handled = true), le decimos
                // a Avalonia: "La misión de esta tecla 'Enter' termina aquí". Esto evita
                // que el control siga procesándola y añada un salto de línea no deseado.
                e.Handled = true;

                // Finalmente, se accede al comando del ViewModel y lo ejecuta.
                if (DataContext is MainViewModel vm && vm.SendMessageCommand.CanExecute(null))
                {
                    vm.SendMessageCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Maneja el clic en el contenido de CUALQUIER diálogo flotante (Configuración, Suscripciones, etc.).
        /// Su única función es marcar el evento como manejado, deteniendo la propagación
        /// del clic para que no llegue al fondo y cierre la ventana.
        /// </summary>
        private void Content_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// Maneja el clic en el overlay de fondo de CUALQUIER diálogo.
        /// Este evento solo se dispara si el clic ocurre fuera del contenido del diálogo.
        /// Comprueba qué ventana está abierta y ejecuta el comando de cierre correspondiente.
        /// </summary>
        private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            // Comprueba qué ventana está abierta y ejecuta su comando de cierre
            if (vm.IsSettingsFlyoutOpen && vm.CloseSettingsFlyoutCommand.CanExecute(null))
            {
                vm.CloseSettingsFlyoutCommand.Execute(null);
            }
            else if (vm.IsSettingsWindowOpen && vm.CloseSettingsWindowCommand.CanExecute(null))
            {
                vm.CloseSettingsWindowCommand.Execute(null);
            }
            else if (vm.IsSubscriptionsWindowOpen && vm.CloseSubscriptionsWindowCommand.CanExecute(null))
            {
                vm.CloseSubscriptionsWindowCommand.Execute(null);
            }
            else if (vm.IsUpdateHistoryWindowOpen && vm.CloseUpdateHistoryWindowCommand.CanExecute(null))
            {
                vm.CloseUpdateHistoryWindowCommand.Execute(null);
            }
        }

        /// <summary>
        /// Maneja el evento de pulsación de teclas para toda la ventana.
        /// Su principal función es detectar la tecla ESC para cerrar cualquier
        /// diálogo o menú flotante que esté abierto actualmente.
        /// </summary>
        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            // Solo nos interesa la tecla Escape
            if (e.Key == Key.Escape)
            {
                // Obtenemos una referencia al ViewModel para poder acceder a los comandos y estados
                if (DataContext is not MainViewModel vm) return;

                // Comproba en orden qué ventana flotante está activa y ejecuta
                // su comando de cierre correspondiente. El orden no es crítico,
                // pero un 'if-else if' asegura que solo se cierre una ventana por pulsación.
                if (vm.IsSettingsFlyoutOpen && vm.CloseSettingsFlyoutCommand.CanExecute(null))
                {
                    vm.CloseSettingsFlyoutCommand.Execute(null);
                }
                else if (vm.IsResetPasswordViewOpen && vm.CloseResetPasswordViewCommand.CanExecute(null))
                {
                    vm.CloseResetPasswordViewCommand.Execute(null);
                }
                else if (vm.IsForgotPasswordViewOpen && vm.CloseForgotPasswordCommand.CanExecute(null))
                {
                    vm.CloseForgotPasswordCommand.Execute(null);
                }
                else if (vm.IsSettingsWindowOpen && vm.CloseSettingsWindowCommand.CanExecute(null))
                {
                    vm.CloseSettingsWindowCommand.Execute(null);
                }
                else if (vm.IsSubscriptionsWindowOpen && vm.CloseSubscriptionsWindowCommand.CanExecute(null))
                {
                    vm.CloseSubscriptionsWindowCommand.Execute(null);
                }
            }
        }
        
        /// <summary>
        /// Manejador del evento de cierre de la ventana.
        /// Se asegura de que se ejecute una limpieza final antes de que la app se cierre.
        /// </summary>
        private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (DataContext is MainViewModel vm) { }
            await Task.CompletedTask;
        }
        #region Implementación de IStorageService

        /// <summary>
        /// Implementación del método de la interfaz para abrir el selector de archivos.
        /// </summary>
        public async Task<IReadOnlyList<IStorageFile>?> PickMultipleFilesAsync()
        {
            // Obtiene el StorageProvider del TopLevel de la ventana actual.
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            return await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Seleccionar Archivos",
                AllowMultiple = true
            });
        }

        #endregion

        #region Lógica de Drag and Drop

        private void DragEnter(object? sender, DragEventArgs e)
        {
            // Este es el primer punto de control. Si este log no aparece
            // cuando arrastras un archivo sobre la ventana, significa que Avalonia
            // o el sistema operativo no están ni siquiera notificando a la app.
            Log.Information("[DragDrop] Evento DragEnter detectado.");

            if (e.Data.Contains(DataFormats.Files))
            {
                Log.Information("[DragDrop] DragEnter confirma que se están arrastrando archivos. Mostrando indicador.");
                DropZoneVisualIndicator.IsVisible = true;
            }
            else
            {
                Log.Warning("[DragDrop] DragEnter detectado, pero no contiene datos en el formato de archivo esperado.");
            }
        }

        private void DragLeave(object? sender, DragEventArgs e)
        {
            // Oculta el indicador visual.
            Log.Information("[DragDrop] Evento DragLeave detectado. Ocultando indicador.");
            DropZoneVisualIndicator.IsVisible = false;
        }

        private async void Drop(object? sender, DragEventArgs e)
        {
            Log.Information("[DragDrop] ¡Evento Drop detectado! El usuario ha soltado los archivos.");
            DropZoneVisualIndicator.IsVisible = false;
            
            // EL TRUCAZO:
            // e.Data.GetFiles() nos devuelve una lista de IStorageItem.
            // Usa LINQ con .OfType<IStorageFile>() para filtrar elegantemente esa
            // lista y quedarnos solo con los elementos que son realmente archivos.
            // Así se asegura de pasarle al ViewModel exactamente el tipo de lista que espera.
            var files = e.Data.GetFiles()?.OfType<IStorageFile>().ToList();

            if (files != null && files.Any() && DataContext is MainViewModel vm)
            {
                Log.Information("[DragDrop] {Count} archivo(s) válidos. Pasando la batuta al MainViewModel.", files.Count);
                // ¡VICTORIA!
                // Ahora la comunicación es perfecta. La Vista ha hecho su trabajo de
                // pre-filtrado y le entrega al Director una lista limpia y correcta.
                await Task.Run(() => vm.HandleDroppedFilesAsync(files));
            }
            else
            {
                Log.Warning("[DragDrop] Evento Drop, pero no se encontraron archivos válidos o el ViewModel no está disponible.");
            }
        }

        #endregion
    }
}
