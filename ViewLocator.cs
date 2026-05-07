using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AsistenteVirtual.ViewModels;

namespace AsistenteVirtual
{
    /// <summary>
    /// Define la lógica para localizar y crear automáticamente una Vista basada en su ViewModel correspondiente.
    /// </summary>
    /// <remarks>
    /// Sigue la convención de nombres: si el ViewModel se llama "EjemploViewModel", 
    /// buscará una clase llamada "EjemploView" en el mismo espacio de nombres.
    /// </remarks>
    public class ViewLocator : IDataTemplate
    {
        /// <summary>
        /// Construye la instancia de la Vista para un objeto de datos (ViewModel) específico.
        /// </summary>
        /// <param name="param">La instancia del ViewModel.</param>
        /// <returns>El control de la Vista instanciado o un mensaje de error si no se encuentra.</returns>
        public Control? Build(object? param)
        {
            if (param is null) { return null; }

            // Convención: Reemplazar "ViewModel" por "View" en el nombre completo del tipo.
            string name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            Type? type = Type.GetType(name);

            return type != null ? (Control)Activator.CreateInstance(type)! : new TextBlock { Text = "No se encontró la Vista para: " + name };
        }

        /// <summary>
        /// Determina si este localizador puede procesar el objeto de datos proporcionado.
        /// </summary>
        /// <param name="data">El objeto a evaluar.</param>
        /// <returns>True si el objeto hereda de <see cref="ViewModelBase"/>.</returns>
        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}