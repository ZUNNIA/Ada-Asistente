namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Define los diferentes niveles de suscripción que un usuario puede tener.
    /// </summary>
    public enum SubscriptionTier
    {
        /// <summary>
        /// Nivel de acceso básico y gratuito.
        /// </summary>
        Free,

        /// <summary>
        /// Nivel de suscripción de pago inicial.
        /// </summary>
        Esencial,

        /// <summary>
        /// Nivel de suscripción intermedio.
        /// </summary>
        Plus,

        /// <summary>
        /// Nivel de suscripción más alto con los mejores límites.
        /// </summary>
        Premium,

        /// <summary>
        /// Un estado especial para usuarios a los que se les ha denegado el acceso.
        /// </summary>
        Ban
    }
}