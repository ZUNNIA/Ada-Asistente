using MessagePack;
namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa los datos de un usuario autenticado en la aplicación.
    /// Contiene la información esencial del perfil obtenida del proveedor de autenticación.
    /// </summary>
    [MessagePackObject(keyAsPropertyName: true)]
    public class User
    {
        /// <summary>
        /// Obtiene o establece la última versión de la aplicación disponible en el servidor.
        /// </summary>
        [Key("LatestAppVersion")]
        public string LatestAppVersion { get; set; } = string.Empty;

        /// <summary>
        /// Obtiene o establece el identificador único del usuario (ej. Google User ID).
        /// </summary>
        [Key("Id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Obtiene o establece el nombre para mostrar del usuario.
        /// </summary>
        [Key("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Obtiene o establece el correo electrónico del usuario.
        /// </summary>
        [Key("Email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Obtiene o establece la URL de la imagen de perfil del usuario.
        /// </summary>
        [Key("ProfilePictureUrl")]
        public string ProfilePictureUrl { get; set; } = string.Empty;

        /// <summary>
        /// Obtiene o establece el nivel de suscripción del usuario.
        /// Por defecto es 'Gratis' hasta que se cargue desde Firestore.
        /// </summary>
        [Key("SubscriptionTier")]
        public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;

        /// <summary>
        /// Obtiene o establece si el usuario está baneado.
        /// </summary>
        [Key("IsBanned")]
        public bool IsBanned { get; set; }
    }
}
