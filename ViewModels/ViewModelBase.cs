using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// Proporciona la implementación base para todos los ViewModels de la aplicación.
    /// </summary>
    /// <remarks>
    /// Centraliza la lógica de notificación de cambios en propiedades (<see cref="INotifyPropertyChanged"/>), 
    /// permitiendo que la interfaz de usuario de Avalonia reaccione automáticamente ante cambios en el estado del modelo.
    /// </remarks>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Evento que se dispara cuando el valor de una propiedad cambia.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Notifica a los suscriptores (normalmente el motor de Binding de Avalonia) que una propiedad ha cambiado.
        /// </summary>
        /// <param name="propertyName">
        /// El nombre de la propiedad. Se utiliza <see cref="CallerMemberNameAttribute"/> para obtener 
        /// automáticamente el nombre del método/propiedad que llama a esta función.
        /// </param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Compara el valor actual de un campo con un nuevo valor y, si son distintos, actualiza el campo y dispara la notificación.
        /// </summary>
        /// <typeparam name="T">El tipo de la propiedad.</typeparam>
        /// <param name="field">Referencia al campo privado (backing field) que almacena el valor.</param>
        /// <param name="newValue">El nuevo valor a asignar.</param>
        /// <param name="propertyName">El nombre de la propiedad para la notificación (inferido automáticamente).</param>
        /// <returns>True si el valor cambió y se notificó; de lo contrario, False.</returns>
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