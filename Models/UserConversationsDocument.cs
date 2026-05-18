using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa la estructura raíz del documento de usuario tal como se recupera del backend.
    /// Consolida el perfil, el historial de conversaciones y el balance de créditos en un solo objeto.
    /// </summary>
    public class UserConversationsDocument
    {
        /// <summary>
        /// Obtiene o establece los datos básicos y de seguridad del perfil del usuario.
        /// </summary>
        [JsonPropertyName("user_profile")] // Nota: Asegúrate de que el backend use este nombre o cámbialo si es necesario
        public UserProfile UserProfile { get; set; } = new UserProfile();

        /// <summary>
        /// Obtiene o establece el historial resumido de conversaciones del usuario.
        /// </summary>
        /// <value>Lista de <see cref="ConversationStoreEntry"/> para cargar el panel lateral.</value>
        [JsonPropertyName("conversations_list")]
        public List<ConversationStoreEntry> Conversations { get; set; } = [];

        /// <summary>
        /// Obtiene o establece la fecha de la última sincronización o modificación del documento.
        /// </summary>
        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Obtiene o establece el saldo actual de créditos destinados a interacciones de chat de texto.
        /// </summary>
        /// <remarks>1 crédito equivale aproximadamente a $0.0001 USD de costo operativo.</remarks>
        [JsonPropertyName("chat_wallet_balance")]
        public long ChatWalletBalance { get; set; } = 0;

        /// <summary>
        /// Obtiene o establece el saldo actual de créditos destinados a la generación de contenido (imágenes/video).
        /// </summary>
        [JsonPropertyName("generation_wallet_balance")]
        public long GenerationWalletBalance { get; set; } = 0;
    }
}