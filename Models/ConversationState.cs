namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Define los posibles estados del ciclo de vida de una conversación.
    /// </summary>
    public enum ConversationState
    {
        /// <summary>
        /// Una conversación normal y en uso.
        /// </summary>
        Active,

        /// <summary>
        /// Una conversación creada en segundo plano, lista para usarse pero aún no activada.
        /// </summary>
        Prewarmed
    }
}
