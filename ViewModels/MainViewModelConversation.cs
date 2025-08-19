using AsistenteVirtual.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsistenteVirtual.ViewModels
{
    public partial class MainViewModel
    {
        //==================================================================//
        // LÓGICA DE CONVERSACIONES (EL ARCHIVO HISTÓRICO)                  //
        //==================================================================//

        /// <summary>
        /// Restablece la aplicación a una nueva conversación en blanco.
        /// </summary>
        private async Task CreateNewConversationAsync()
        {
            if (_currentConversationHistoryViewModel != null)
                _currentConversationHistoryViewModel.IsSelected = false;

            _currentConversationHistoryViewModel = null;
            _isPendingNewConversation = true;
            ChatMessages.Clear();
            AttachedFiles.Clear();
            TemporaryAttachedFiles.Clear();
            // ADVERTENCIA: Se necesita limpiar los archivos temporales que se hayan subido
            // pero que pertenecen a la conversación que se está abandonando.
            await Task.CompletedTask; 
        }
        
        /// <summary>
        /// Carga una conversación seleccionada del historial.
        /// </summary>
        /// <param name="vm">El ViewModel de la conversación a cargar.</param>
        private async Task LoadSelectedConversationAsync(ConversationHistoryViewModel? vm)
        {
            if (vm == null || _currentConversationHistoryViewModel == vm || IsLoadingConversation) return;

            IsLoadingConversation = true;
            try
            {
                foreach (var item in ConversationHistory) item.IsSelected = false;
                vm.IsSelected = true;
                _currentConversationHistoryViewModel = vm;
                _isPendingNewConversation = false;

                await _dispatcher.InvokeAsync(() => {
                    ChatMessages.Clear();
                    AttachedFiles.Clear();
                    TemporaryAttachedFiles.Clear();
                });
                await Task.WhenAll(LoadMessagesForVMAsync(vm), LoadFilesForVMAsync(vm));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al cargar la conversación: {ThreadId}", vm?.ThreadId);
                _notificationService.ShowTemporaryNotification("No se pudo cargar la conversación.", true);
                await CreateNewConversationAsync();
            }
            finally
            {
                IsLoadingConversation = false;
            }
        }

        /// <summary>
        /// Carga los mensajes de una conversación desde el backend.
        /// </summary>
        private async Task LoadMessagesForVMAsync(ConversationHistoryViewModel vm)
        {
            if (string.IsNullOrEmpty(vm.ThreadId) || CurrentUser == null) return;
            string? userToken = await _authService.GetCurrentUserTokenAsync();
            if (string.IsNullOrEmpty(userToken)) throw new Exception("Token de usuario no disponible.");

            List<ChatMessage> messages = await _backendService.LoadMessagesAsync(userToken, vm.ThreadId);
            await _dispatcher.InvokeAsync(() => {
                foreach (var msg in messages)
                {
                    msg.Role = msg.Role == "user" ? "Usuario" : "Asistente";
                    ChatMessages.Add(new ChatMessageViewModel(msg));
                }
            });
        }

        /// <summary>
        /// Procesa la edición del título de una conversación y la guarda.
        /// </summary>
        /// <param name="vm">El ViewModel de la conversación cuyo título se está editando.</param>
        public async Task ProcessTitleEditAsync(ConversationHistoryViewModel vm)
        {
            var newTitle = vm.Title.Trim();
            if (string.IsNullOrWhiteSpace(newTitle))
                vm.Title = vm.OriginalTitle;
            
            vm.IsEditingTitle = false;

            if (CurrentUser != null && vm.Title != vm.OriginalTitle)
            {
                string? userToken = await _authService.GetCurrentUserTokenAsync();
                if (!string.IsNullOrEmpty(userToken))
                    await _backendService.SaveConversationAsync(userToken, vm.GetModel());
            }
        }

        /// <summary>
        /// Orquesta la eliminación completa de una conversación.
        /// </summary>
        /// <param name="vm">El ViewModel de la conversación a eliminar.</param>
        private async Task DeleteConversationAsync(ConversationHistoryViewModel vm)
        {
            if (CurrentUser == null) return;
            
            string threadIdToDelete = vm.ThreadId;
            string? vectorStoreIdToDelete = vm.GetModel().VectorStoreId;

            await _dispatcher.InvokeAsync(() => ConversationHistory.Remove(vm));
            if (_currentConversationHistoryViewModel == vm)
                await CreateNewConversationAsync();

            string? userToken = await _authService.GetCurrentUserTokenAsync();
            if (string.IsNullOrEmpty(userToken)) return;

            await _backendService.DeleteConversationAsync(userToken, threadIdToDelete);
            if (!string.IsNullOrEmpty(threadIdToDelete))
                _ = _backendService.DeleteConversationResourcesAsync(userToken, threadIdToDelete, vectorStoreIdToDelete);

            _notificationService.ShowTemporaryNotification($"Conversación '{vm.Title}' eliminada.", false);
        }

        /// <summary>
        /// Asegura que exista una conversación activa antes de enviar un mensaje.
        /// Si se está empezando un chat, crea una nueva entrada en el historial.
        /// </summary>
        private async Task EnsureActiveConversationAsync(string firstUserMessage)
        {
            string title = firstUserMessage.Length > 40 ? firstUserMessage[..37] + "..." : firstUserMessage;
            if (string.IsNullOrWhiteSpace(title)) title = $"Chat del {DateTime.Now:g}";

            var conversation = new Conversation { Title = title, State = ConversationState.Active };
            var vm = new ConversationHistoryViewModel(
                conversation, LoadConversationCommand, ProcessTitleEditCommand,
                (renameVM) => renameVM.IsEditingTitle = true,
                async (deleteVM) => await DeleteConversationAsync(deleteVM)
            );

            foreach (var item in ConversationHistory) item.IsSelected = false;
            vm.IsSelected = true;
            _currentConversationHistoryViewModel = vm;
            await _dispatcher.InvokeAsync(() => ConversationHistory.Insert(0, vm));
            
            if (IsUserLoggedIn && CurrentUser != null)
            {
                string? userToken = await _authService.GetCurrentUserTokenAsync();
                if (!string.IsNullOrEmpty(userToken))
                    await _backendService.SaveConversationAsync(userToken, conversation);
            }
            _isPendingNewConversation = false;
        }
    }
}