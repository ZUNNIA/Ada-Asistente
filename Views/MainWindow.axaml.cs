using AsistenteVirtual.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Threading;
using AsistenteVirtual.Services.Interfaces;

namespace AsistenteVirtual.Views
{
    /// <summary>
    /// Ventana principal de la aplicación. Orquesta la navegación global, las animaciones de los paneles laterales
    /// y proporciona acceso a los servicios de almacenamiento del sistema operativo.
    /// </summary>
    /// <remarks>
    /// Implementa <see cref="IStorageService"/> para permitir que los ViewModels soliciten archivos sin acoplarse a la API de Avalonia.
    /// Utiliza un sistema de <see cref="DispatcherTimer"/> para gestionar el cierre retardado de paneles, mejorando la experiencia de usuario (UX).
    /// </remarks>
    public partial class MainWindow : Window, IStorageService, IDisposable
    {
        /// <summary>
        /// Define los posibles estados de un panel lateral para controlar las animaciones.
        /// </summary>
        private enum PanelState { Closed, Opening, Open, Closing }
        private PanelState _leftPanelState = PanelState.Closed;
        private PanelState _rightPanelState = PanelState.Closed;

        /// <summary>
        /// Temporizador para retrasar el cierre del panel izquierdo cuando el puntero sale de su área de activación.
        /// </summary>
        private readonly DispatcherTimer _leftCloseTimer;

        /// <summary>
        /// Temporizador para retrasar el cierre del panel derecho.
        /// </summary>
        private readonly DispatcherTimer _rightCloseTimer;

        /// <summary>
        /// Token de cancelación para detener animaciones en curso del panel izquierdo si el usuario
        /// realiza una nueva acción (ej. vuelve a entrar en el área antes de que se cierre).
        /// </summary>
        private CancellationTokenSource? _leftAnimationCts;

        /// <summary>
        /// Token de cancelación para las animaciones del panel derecho.
        /// </summary>
        private CancellationTokenSource? _rightAnimationCts;

        /// <summary>
        /// Inicializa la ventana y configura los temporizadores de control de UI.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Registro de evento Drop para la ventana completa
            AddHandler(DragDrop.DropEvent, Drop);

            Opened += OnWindowOpened;
            Closing += OnWindowClosing;
            KeyDown += MainWindow_KeyDown;
            PointerExited += (s, e) =>
            {
                if (!_leftCloseTimer.IsEnabled) { _leftCloseTimer.Start(); }
                if (!_rightCloseTimer.IsEnabled) { _rightCloseTimer.Start(); }
            };

            // Configuración de temporizadores para el cierre suave de paneles laterales
            _leftCloseTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, OnLeftCloseTimerTick) { IsEnabled = false };
            _rightCloseTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, OnRightCloseTimerTick) { IsEnabled = false };
        }

        #region Implementación de IStorageService

        /// <summary>
        /// Abre el diálogo del sistema operativo para que el usuario seleccione uno o más archivos.
        /// </summary>
        /// <returns>
        /// Una lista de solo lectura de objetos IStorageFile que representan los archivos seleccionados,
        /// o null si el usuario cancela la operación.
        /// </returns>
        public async Task<IReadOnlyList<IStorageFile>?> PickMultipleFilesAsync()
        {
            TopLevel? topLevel = GetTopLevel(this);
            return topLevel == null
                ? null
                : await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Seleccionar Archivos", AllowMultiple = true });
        }

        /// <summary>
        /// Lanza el selector de carpetas nativo.
        /// </summary>
        /// <returns>La carpeta seleccionada o null.</returns>
        public async Task<IStorageFolder?> PickFolderAsync()
        {
            TopLevel? topLevel = GetTopLevel(this);
            if (topLevel?.StorageProvider == null) { return null; }
            IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Seleccionar Carpeta", AllowMultiple = false });
            return folders.Count > 0 ? folders[0] : null;
        }

        #endregion

        #region Manejadores de Animación y Timers

        /// <summary>
        /// Ejecuta el tick del temporizador para cerrar el panel izquierdo (Historial).
        /// </summary>
        private void OnLeftCloseTimerTick(object? sender, EventArgs e)
        {
            _leftCloseTimer.Stop();
            HistoryPanel? panel = this.FindControl<HistoryPanel>("HistoryPanelControl");
            if (panel != null && (_leftPanelState == PanelState.Open))
            {
                if (!panel.IsPointerOver)
                {
                    _leftAnimationCts?.Cancel();
                    _leftAnimationCts = new CancellationTokenSource();
                    _leftPanelState = PanelState.Closing;
                    _ = AnimatePanelAsync(panel, -250, _leftAnimationCts.Token)
                        .ContinueWith(_ => _leftPanelState = PanelState.Closed, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        /// <summary>
        /// Ejecuta el tick del temporizador para cerrar el panel derecho (Archivos).
        /// </summary>
        private void OnRightCloseTimerTick(object? sender, EventArgs e)
        {
            _rightCloseTimer.Stop();
            FilesPanel? panel = this.FindControl<FilesPanel>("FilesPanelControl");
            if (panel != null && !panel.IsPointerOver && _rightPanelState == PanelState.Open)
            {
                _rightAnimationCts?.Cancel();
                _rightAnimationCts = new CancellationTokenSource();
                _rightPanelState = PanelState.Closing;
                _ = AnimatePanelAsync(panel, 250, _rightAnimationCts.Token).ContinueWith(_ => _rightPanelState = PanelState.Closed, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        /// <summary>
        /// Orquesta la animación de transformación de un panel lateral.
        /// </summary>
        private static async Task AnimatePanelAsync(Control control, double targetX, CancellationToken token)
        {
            if (control.RenderTransform is not TranslateTransform)
            {
                control.RenderTransform = new TranslateTransform();
            }

            Animation animation = new()
            {
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, targetX) } } }
            };

            try { await animation.RunAsync(control, token); } catch (OperationCanceledException) { }
        }

        #endregion

        #region Eventos de Ventana y Drag & Drop

        /// <summary>
        /// Maneja la recepción de archivos soltados sobre cualquier área de la ventana principal.
        /// </summary>
        private async void Drop(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files) && DataContext is MainViewModel vm)
            {
                IEnumerable<IStorageItem>? items = e.Data.GetFiles();
                if (items != null)
                {
                    await vm.ChatVM.HandleDroppedItemsAsync(items);
                }
            }
        }

        private async void OnWindowOpened(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.InitializeAsync();
            }
        }

        private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.CleanupOnExitAsync();
            }
        }

        /// <summary>
        /// Permite cerrar cualquier overlay activo presionando la tecla 'Escape'.
        /// </summary>
        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DataContext is MainViewModel vm)
            {
                // Si la imagen está abierta, la cerramos
                if (vm.UI.IsImageFullscreenOpen)
                {
                    vm.UI.IsImageFullscreenOpen = false;
                }
                else
                {
                    vm.CloseActiveOverlayCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Cierra cualquier overlay activo (ventanas modales, flyouts) si se hace clic
        /// en el fondo oscuro.
        /// </summary>
        private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.UI.IsImageFullscreenOpen = false; // Cierra la imagen
                vm.CloseActiveOverlayCommand.Execute(null); // Cierra otros overlays
            }
        }

        /// <summary>
        /// Evita que un clic en el contenido de un overlay (como una ventana modal)
        /// se propague al fondo y lo cierre.
        /// </summary>
        private void Content_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
        }

        #endregion

        #region Control de Mouse para Paneles

        private void HistoryTriggerArea_PointerEntered(object? sender, PointerEventArgs e)
        {
            _leftCloseTimer.Stop();
            if (_leftPanelState is PanelState.Closed or PanelState.Closing)
            {
                _leftAnimationCts?.Cancel();
                _leftAnimationCts = new CancellationTokenSource();
                _leftPanelState = PanelState.Opening;
                HistoryPanel? panel = this.FindControl<HistoryPanel>("HistoryPanelControl");
                if (panel != null)
                {
                    _ = AnimatePanelAsync(panel, 0, _leftAnimationCts.Token).ContinueWith(_ => _leftPanelState = PanelState.Open, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        private void HistoryTriggerArea_PointerExited(object? sender, PointerEventArgs e)
        {
            _leftCloseTimer.Start();
        }

        private void HistoryPanel_PointerEntered(object? sender, PointerEventArgs e)
        {
            _leftCloseTimer.Stop();
        }

        private void HistoryPanel_PointerExited(object? sender, PointerEventArgs e)
        {
            _leftCloseTimer.Start();
        }

        private void FilesTriggerArea_PointerEntered(object? sender, PointerEventArgs e)
        {
            _rightCloseTimer.Stop();
            if (_rightPanelState is PanelState.Closed or PanelState.Closing)
            {
                _rightAnimationCts?.Cancel();
                _rightAnimationCts = new CancellationTokenSource();
                _rightPanelState = PanelState.Opening;
                FilesPanel? panel = this.FindControl<FilesPanel>("FilesPanelControl");
                if (panel != null)
                {
                    _ = AnimatePanelAsync(panel, 0, _rightAnimationCts.Token).ContinueWith(_ => _rightPanelState = PanelState.Open, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        private void FilesTriggerArea_PointerExited(object? sender, PointerEventArgs e)
        {
            _rightCloseTimer.Start();
        }

        private void FilesPanel_PointerEntered(object? sender, PointerEventArgs e)
        {
            _rightCloseTimer.Stop();
        }

        private void FilesPanel_PointerExited(object? sender, PointerEventArgs e)
        {
            _rightCloseTimer.Start();
        }

        #endregion

        public void Dispose()
        {
            _leftAnimationCts?.Dispose();
            _rightAnimationCts?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}