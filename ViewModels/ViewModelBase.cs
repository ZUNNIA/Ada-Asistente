using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// Una clase base para todos los ViewModels que implementa la interfaz INotifyPropertyChanged.
    /// Proporciona una forma estandarizada de notificar a la Vista sobre cambios en las propiedades.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Evento que se dispara cuando cambia el valor de una propiedad.
        /// La Vista (Avalonia) se suscribe a este evento para actualizar la UI automáticamente.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Notifica a los suscriptores que el valor de una propiedad ha cambiado.
        /// </summary>
        /// <param name="propertyName">El nombre de la propiedad que cambió. 
        /// Este parámetro es opcional y se infiere automáticamente por el compilador.</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Método auxiliar para establecer el valor de una propiedad y notificar el cambio si el valor es diferente.
        /// Esto evita notificaciones innecesarias y código repetitivo en las propiedades de los ViewModels.
        /// </summary>
        /// <typeparam name="T">El tipo de la propiedad.</typeparam>
        /// <param name="field">Referencia al campo de respaldo de la propiedad.</param>
        /// <param name="newValue">El nuevo valor para la propiedad.</param>
        /// <param name="propertyName">El nombre de la propiedad. Se infiere automáticamente.</param>
        /// <returns>True si el valor cambió, false en caso contrario.</returns>
        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, newValue))
            {
                return false;
            }

            field = newValue;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
