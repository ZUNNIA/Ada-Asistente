namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Define los estados lógicos por los que puede pasar una conversación en el sistema.
    /// </summary>
    public enum ConversationState
    {
        /// <summary>
        /// La conversación ha sido iniciada por el usuario y contiene al menos un mensaje persistido.
        /// </summary>
        Active,

        /// <summary>
        /// Estado de "pre-calentamiento". La conversación existe en DB para permitir subida de archivos,
        /// pero el usuario aún no ha enviado el primer mensaje de texto.
        /// </summary>
        Prewarmed
    }
}
