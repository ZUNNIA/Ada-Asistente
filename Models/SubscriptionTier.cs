namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Define los diferentes niveles de suscripción que un usuario puede tener.
    /// </summary>
    public enum SubscriptionTier
    {
        /// <summary>
        /// Nivel de acceso básico para usuarios sin suscripción activa.
        /// </summary>
        Free,

        /// <summary>
        /// Nivel de suscripción de pago "Escencial".
        /// </summary>
        Escencial,

        /// <summary>
        /// Nivel de suscripción de pago "Plus".
        /// </summary>
        Plus,

        /// <summary>
        /// Nivel de suscripción de pago "Pro".
        /// </summary>
        Pro,

        /// <summary>
        /// Un estado especial para usuarios a los que se les ha denegado el acceso.
        /// </summary>
        Ban
    }
}