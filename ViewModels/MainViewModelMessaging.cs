using AsistenteVirtual.Commands;
using AsistenteVirtual.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace AsistenteVirtual.ViewModels
{
    public partial class MainViewModel
    {
        //==================================================================//
        // LÓGICA DE MENSAJERÍA (EL DIÁLOGO PRINCIPAL)                      //
        //==================================================================//

        /// <summary>
        /// Orquesta el envío de la entrada del usuario al backend para su procesamiento.
        /// Este es el movimiento principal de la sinfonía.
        /// </summary>
        /// <summary>
        /// Orquesta el envío de la entrada del usuario al backend para su procesamiento.
        /// Este es el movimiento principal de la sinfonía.
        /// </summary>
        private async Task SendMessageAsync()
        {
            //==================================================================//
            // PASO 1: VALIDACIÓN Y PREPARACIÓN                                 //
            //==================================================================//
            var failedUpload = TemporaryAttachedFiles.FirstOrDefault(f => f.State == UploadState.Failed);
            if (failedUpload != null)
            {
                _notificationService.ShowTemporaryNotification($"El archivo '{failedUpload.FileName}' no se pudo subir. Por favor, quítalo e intenta de nuevo.", true);
                return;
            }

            string userTextToSend = UserInputText.Trim();
            var filesToProcess = TemporaryAttachedFiles.ToList();

            if (string.IsNullOrWhiteSpace(userTextToSend) && !filesToProcess.Any())
            {
                _notificationService.ShowTemporaryNotification("Para empezar a conversar, escribe un mensaje o adjunta un archivo.", false);
                return;
            }

            if (IsProcessingLogin)
            {
                Log.Information("[SendMessage] App está inicializando. Mensaje de usuario encolado (Longitud: {Length}, Archivos: {FileCount}).", userTextToSend.Length, filesToProcess.Count);
                var queuedOptimisticMessage = new ChatMessageViewModel(new ChatMessage { Role = "Usuario", Content = userTextToSend });
                ChatMessages.Add(queuedOptimisticMessage);
                _pendingMessagesQueue.Enqueue(new Tuple<string, List<AttachedFileViewModel>, ChatMessageViewModel>(userTextToSend, filesToProcess, queuedOptimisticMessage));
                UserInputText = string.Empty;
                TemporaryAttachedFiles.Clear();
                _notificationService.ShowTemporaryNotification("Conectando... tu mensaje se enviará en un momento.", false);
                return;
            }
            if (CurrentUser == null)
            {
                _notificationService.ShowTemporaryNotification("Debes iniciar sesión para poder conversar.", true);
                return;
            }

            if (IsAssistantResponding) await StopResponseAsync();
            IsAssistantResponding = true;

            //==================================================================//
            // PASO 2: ACTUALIZACIÓN OPTIMISTA DE LA UI                         //
            //==================================================================//
            var optimisticMessage = new ChatMessageViewModel(new ChatMessage { Role = "Usuario", Content = userTextToSend });
            ChatMessages.Add(optimisticMessage);
            
            var originalInputText = UserInputText;
            UserInputText = string.Empty;
            TemporaryAttachedFiles.Clear();

            try
            {
                //==================================================================//
                // PASO 3: DELEGAR AL BACKEND Y PROCESAR EL STREAM                  //
                //==================================================================//
                await ProcessAndStreamMessageAsync(userTextToSend, filesToProcess, optimisticMessage, originalInputText);
            }
            catch (OperationCanceledException)
            {
                Log.Information("[SendMessage] Envío cancelado por el usuario.");
                RollbackFailedUserMessage(optimisticMessage, originalInputText, filesToProcess);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SendMessage] Error durante el proceso de envío. Analizando causa...");
                
                string errorMessage = ex.Message.ToLower();

                // Caso 1: El error es sobre límites de uso o sesión expirada.
                // Es un mensaje informativo del servidor, no un error de red.
                if (errorMessage.Contains("límite") || errorMessage.Contains("suscripción") || errorMessage.Contains("expirado"))
                {
                    _notificationService.ShowTemporaryNotification(ex.Message, true);
                    if (errorMessage.Contains("expirado"))
                    {
                        await LogoutAsync();
                    }
                }
                // Caso 2: Cualquier otro error. Lo trata como un fallo de conexión
                // y le da al usuario la opción de reintentar.
                else
                {
                    ErrorNotificationViewModel? notification = null;
                    var retryCommand = new RelayCommand(async _ => {
                        if (notification != null)
                        {
                            notification.ShowProgressBar = true; 
                            await ProcessAndStreamMessageAsync(userTextToSend, filesToProcess, optimisticMessage, originalInputText);
                            ErrorNotifications.Remove(notification);
                        }
                    });

                    notification = new ErrorNotificationViewModel
                    {
                        Message = "No se pudo conectar. ¿Quieres intentarlo de nuevo?",
                        IsCritical = true,
                        IsRetriable = true,
                        RetryCommand = retryCommand
                    };
                    ErrorNotifications.Add(notification);
                }

                RollbackFailedUserMessage(optimisticMessage, originalInputText, filesToProcess);
            }
            finally
            {
                IsAssistantResponding = false;
            }
        }

        /// <summary>
        /// Contiene la lógica para comunicarse con el backend, procesar el streaming de la
        /// respuesta y manejar los posibles errores o cancelaciones.
        /// </summary>
        /// <param name="userText">El texto del mensaje a enviar.</param>
        /// <param name="files">La lista de archivos adjuntos a procesar.</param>
        /// <param name="optimisticMessage">El ViewModel del mensaje que ya se mostró en la UI.</param>
        /// <param name="originalInputTextForRollback">El texto original para restaurarlo si algo falla.</param>
        private async Task ProcessAndStreamMessageAsync(string userText, List<AttachedFileViewModel> files, ChatMessageViewModel optimisticMessage, string originalInputTextForRollback)
        {
            if (IsAssistantResponding) await StopResponseAsync();
            IsAssistantResponding = true;

            try
            {
                string? userToken = await _authService.GetCurrentUserTokenAsync();
                if (string.IsNullOrEmpty(userToken))
                    throw new InvalidOperationException("No se pudo obtener el token de autenticación. Por favor, reinicia sesión.");

                _responseCts = new CancellationTokenSource();
                List<string> newFileIds = await UploadAndGetFileIdsAsync(files, _responseCts.Token);

                if (_isPendingNewConversation || _currentConversationHistoryViewModel == null)
                {
                    await EnsureActiveConversationAsync(userText);
                }
                
                var conversationModel = _currentConversationHistoryViewModel!.GetModel();
                if (newFileIds.Any())
                {
                    conversationModel.AssociatedFileIds.AddRange(newFileIds.Except(conversationModel.AssociatedFileIds));
                    var filesDetails = await _backendService.GetFileDetailsAsync(userToken, newFileIds);
                    await _dispatcher.InvokeAsync(() => {
                        foreach (var file in filesDetails)
                            AttachedFiles.Add(new AttachedFileViewModel(file, _authService, _backendService));
                    });
                }
                
                var assistantMessageVM = new ChatMessageViewModel(new ChatMessage { Role = "Asistente" });
                await _dispatcher.InvokeAsync(() => ChatMessages.Add(assistantMessageVM));

                var stream = _backendService.StreamMessageToBackendAsync(
                    userToken, userText, GetCurrentModeFeatureName(), 
                    conversationModel.ThreadId, newFileIds, conversationModel.VectorStoreId);

                await foreach (var responseChunk in stream.WithCancellation(_responseCts.Token))
                {
                    if (!string.IsNullOrEmpty(responseChunk.TextChunk))
                    {
                        await _dispatcher.InvokeAsync(() => assistantMessageVM.Content += responseChunk.TextChunk);
                    }
                    else if (responseChunk.IsDone)
                    {
                        bool needsSave = false;
                        if (conversationModel.ThreadId != responseChunk.ThreadId && responseChunk.ThreadId is not null)
                        {
                            conversationModel.ThreadId = responseChunk.ThreadId;
                            needsSave = true;
                        }
                        if (!string.IsNullOrEmpty(responseChunk.VectorStoreId) && string.IsNullOrEmpty(conversationModel.VectorStoreId))
                        {
                            conversationModel.VectorStoreId = responseChunk.VectorStoreId;
                            needsSave = true;
                        }
                        if (needsSave)
                        {
                            await _backendService.SaveConversationAsync(userToken, conversationModel);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Information("[SendMessage] Envío cancelado por el usuario.");
                RollbackFailedUserMessage(optimisticMessage, originalInputTextForRollback, files);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SendMessage] Error durante el proceso de envío y streaming.");
                _notificationService.ShowTemporaryNotification(ex.Message, true);
                RollbackFailedUserMessage(optimisticMessage, originalInputTextForRollback, files);
                if (ex.Message.Contains("expirado"))
                {
                    await LogoutAsync();
                }
            }
            finally
            {
                IsAssistantResponding = false;
            }
        }
    }
}
