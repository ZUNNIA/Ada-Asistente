using AsistenteVirtual.Models;
using AsistenteVirtual.ViewModels;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AsistenteVirtual.Services.Interfaces;

namespace AsistenteVirtual.Services.Implementations
{
    /// <summary>
    /// Servicio especializado en la gestión del flujo de mensajes y la orquestación de la inteligencia artificial.
    /// </summary>
    /// <remarks>
    /// Implementa un patrón de "Actualización Optimista" para mejorar la percepción de velocidad del usuario,
    /// añadiendo el mensaje a la UI antes de confirmar su persistencia. Gestiona el ciclo de vida del streaming,
    /// la sincronización de archivos pendientes de subida y la gestión de versiones en mensajes editados.
    /// </remarks>
    /// <remarks>
    /// Inicializa una nueva instancia de <see cref="ChatService"/> con sus dependencias.
    /// </remarks>
    /// <param name="conversationService">Servicio para persistencia de hilos y mensajes.</param>
    /// <param name="notificationService">Servicio para mostrar alertas visuales.</param>
    /// <param name="authService">Servicio para gestión de identidad y tokens.</param>
    /// <param name="dispatcher">Dispatcher de la UI para asegurar hilos seguros al modificar colecciones.</param>
    public class ChatService(
        IConversationService conversationService,
        INotificationService notificationService,
        IAuthService authService,
        Dispatcher dispatcher) : ViewModelBase, IChatService, IDisposable
    {
        private readonly IConversationService _conversationService = conversationService;
        private readonly INotificationService _notificationService = notificationService;
        private readonly IAuthService _authService = authService;
        private readonly Dispatcher _dispatcher = dispatcher;

        private CancellationTokenSource? _responseCts;
        private bool _isAssistantResponding;

        /// <summary>
        /// Indica si el asistente está actualmente procesando una respuesta o generando contenido.
        /// Se utiliza habitualmente para deshabilitar controles de entrada en la interfaz.
        /// </summary>
        public bool IsAssistantResponding
        {
            get => _isAssistantResponding;
            private set => SetProperty(ref _isAssistantResponding, value);
        }


        /// <summary>
        /// Orquesta el envío de mensajes y la recepción de respuestas de IA en un hilo de fondo.
        /// Garantiza que la interfaz de usuario (UI Thread) nunca se bloquee, delegando el procesamiento pesado a Task.Run.
        /// </summary>
        /// <param name="context">El contexto del mensaje que contiene las colecciones y datos necesarios para el envío.</param>
        /// <returns>Una tarea asíncrona que representa el ciclo de vida completo de la interacción.</returns>
        public async Task SendMessageAsync(MessageContext context)
        {
            string userTextToSend = context.UserInput.Trim();

            // Validación rápida antes de saltar a hilos secundarios
            if (string.IsNullOrWhiteSpace(userTextToSend) && context.TemporaryFiles.Count == 0) { return; }

            // Desplazamos toda la ejecución a un hilo del ThreadPool para liberar la UI inmediatamente
            await Task.Run(async () =>
            {
                _responseCts = new CancellationTokenSource();
                CancellationToken token = _responseCts.Token;
                try

                {
                    string? userToken = await _authService.GetCurrentUserTokenAsync();
                    List<AttachedFileViewModel> filesToProcess = [.. context.TemporaryFiles];

                    // Actualización visual inicial (UI Thread)
                    await _dispatcher.InvokeAsync(() =>
                    {
                        IsAssistantResponding = true;
                        if (!context.IsEditing)
                        {
                            ChatMessageViewModel optimisticMessage = new(new ChatMessage { Role = "Usuario", Content = userTextToSend }, MessageStatus.Sending);
                            context.ChatMessages.Add(optimisticMessage);
                        }
                    });

                    // Gestión de la conversación
                    Conversation? conversationModel = context.CurrentConversation?.GetModel();
                    conversationModel ??= await context.EnsureActiveConversationAsync.Invoke(userTextToSend, filesToProcess);

                    // Busca el mensaje del usuario que se quiere persistir
                    ChatMessageViewModel? userMsgVm = context.IsEditing
                        ? context.ChatMessages.LastOrDefault(m => m.Role == "Usuario" && !m.IsEditing) // El que acaba de editar
                        : context.ChatMessages.Last(m => m.Role == "Usuario");

                    if (userMsgVm != null)
                    {
                        // Esto actualizará el documento en Firestore si ya tiene ID, o lo creará si no.
                        await _conversationService.SaveMessageAsync(userToken!, conversationModel.ConversationId, userMsgVm.GetModel());
                    }

                    // Preparación del asistente
                    ChatMessageViewModel? assistantMsgVm = null;
                    await _dispatcher.InvokeAsync(() =>
                    {
                        assistantMsgVm = new ChatMessageViewModel(new ChatMessage
                        {
                            Role = "Asistente",
                            Content = "...",
                            ParentVersionIndex = userMsgVm?.CurrentVersionIndex ?? 0
                        });
                        context.ChatMessages.Add(assistantMsgVm);
                    });

                    List<ChatMessage> history = [.. context.ChatMessages
                        .Select(m => m.GetModel())
                        .Where(m => !string.IsNullOrEmpty(m.Content))];

                    // Consumo del Stream de IA (Background)
                    IAsyncEnumerable<StreamedBackendResponse> stream = _conversationService.StreamMessageToBackendAsync(
                        userToken!, userTextToSend, context.FeatureName, context.UnderlyingMode, history,
                        conversationModel!.AssociatedFiles, conversationModel.ConversationId);

                    await foreach (StreamedBackendResponse chunk in stream)
                    {
                        if (token.IsCancellationRequested) { break; }
                        _dispatcher.Post(() =>
                        {
                            if (chunk.Type == "chunk")
                            {
                                // Si es el primer texto real, quitamos los puntos suspensivos
                                if (assistantMsgVm!.Content == "...")
                                {
                                    assistantMsgVm.Content = "";
                                }
                                assistantMsgVm!.Content += chunk.Content;
                            }
                        }, DispatcherPriority.Background);
                    }

                    // Persistencia final (Background)
                    await _conversationService.SaveMessageAsync(userToken!, conversationModel.ConversationId, assistantMsgVm!.GetModel());
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[ChatService] Fallo crítico en el flujo de mensaje");
                    _notificationService.ShowTemporaryNotification("Error de conexión con el asistente", true);
                }
                finally
                {
                    _dispatcher.Post(() => IsAssistantResponding = false);
                }
            });
        }

        /// <summary>
        /// Cancela la petición de streaming actual mediante el token de cancelación.
        /// </summary>
        /// <returns>Una tarea completada.</returns>
        public Task StopResponseAsync()
        {
            _responseCts?.Cancel();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Libera los recursos utilizados por el servicio, incluyendo tokens de cancelación.
        /// </summary>
        public void Dispose()
        {
            _responseCts?.Cancel();
            _responseCts?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}