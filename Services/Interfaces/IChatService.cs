using AsistenteVirtual.Models;
using AsistenteVirtual.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace AsistenteVirtual.Services.Interfaces
{
    /// <summary>
    /// Define el contrato para el motor de orquestación de la interfaz de chat y la IA.
    /// </summary>
    /// <remarks>
    /// Actúa como el puente entre los ViewModels y los servicios de infraestructura,
    /// gestionando la lógica de "UI Optimista" y el ciclo de vida del streaming.
    /// </remarks>
    public interface IChatService : INotifyPropertyChanged
    {
        /// <summary>
        /// Indica si el asistente está actualmente procesando una petición o generando contenido.
        /// </summary>
        bool IsAssistantResponding { get; }

        /// <summary>
        /// Ejecuta el flujo completo de envío de un mensaje, validaciones y recepción de respuesta.
        /// </summary>
        /// <param name="context">Encapsulación del estado necesario para el envío (<see cref="MessageContext"/>).</param>
        /// <returns>Tarea que representa la finalización del ciclo de respuesta.</returns>
        Task SendMessageAsync(MessageContext context);

        /// <summary>
        /// Interrumpe inmediatamente la generación de la respuesta actual mediante el token de cancelación.
        /// </summary>
        /// <returns>Tarea que representa la cancelación de la operación.</returns>
        Task StopResponseAsync();
    }

    /// <summary>
    /// Encapsula el conjunto de datos y delegados necesarios para que el servicio de chat procese un mensaje.
    /// </summary>
    public class MessageContext
    {
        /// Texto crudo ingresado por el usuario en el editor.
        public string UserInput { get; init; } = string.Empty;

        /// Colección de archivos temporales que el usuario desea adjuntar.
        public ICollection<AttachedFileViewModel> TemporaryFiles { get; init; } = [];

        /// Colección observable de mensajes para realizar actualizaciones visuales en tiempo real.
        public ObservableCollection<ChatMessageViewModel> ChatMessages { get; init; } = [];

        /// La conversación activa. Si es null, el servicio creará un nuevo hilo automáticamente.
        public ConversationHistoryViewModel? CurrentConversation { get; set; }

        /// Nombre funcional de la característica (ej. "QuickMode").
        public string FeatureName { get; init; } = string.Empty;

        /// Identificador técnico del modelo de IA a invocar.
        public string UnderlyingMode { get; init; } = string.Empty;

        /// Indica si la petición actual es una edición de un mensaje enviado previamente.
        public bool IsEditing { get; init; }

        /// <summary>
        /// Delegado para asegurar que la conversación esté activa en la base de datos antes de enviar mensajes.
        /// </summary>
        public System.Func<string, List<AttachedFileViewModel>, Task<Conversation?>> EnsureActiveConversationAsync { get; init; } = (s, l) => Task.FromResult<Conversation?>(null);
    }
}