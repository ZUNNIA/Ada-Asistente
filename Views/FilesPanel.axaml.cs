using Avalonia.Controls;
using Avalonia.Input;
using AsistenteVirtual.ViewModels;
using System.Collections.Generic;
using Avalonia.Platform.Storage;

namespace AsistenteVirtual.Views
{
    /// <summary>
    /// Control que gestiona el panel lateral de visualización y gestión de archivos adjuntos.
    /// </summary>
    /// <remarks>
    /// Implementa soporte nativo para Drag-and-Drop, permitiendo al usuario arrastrar archivos 
    /// directamente sobre el panel para cargarlos en la conversación.
    /// </remarks>
    public partial class FilesPanel : UserControl
    {
        public FilesPanel()
        {
            InitializeComponent();
            AddHandler(DragDrop.DropEvent, OnDrop);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
        }

        /// <summary>
        /// Define visualmente si los elementos arrastrados son aceptables (archivos).
        /// </summary>
        private void OnDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>
        /// Recupera la lista de archivos soltados y la delega al ViewModel para su procesamiento asíncrono.
        /// </summary>
        private async void OnDrop(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            if (DataContext is ChatViewModel vm)
            {
                IEnumerable<IStorageItem>? items = e.Data.GetFiles();
                if (items != null)
                {
                    await vm.HandleDroppedItemsAsync(items);
                }
            }
        }
    }
}