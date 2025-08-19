using Google.Cloud.Firestore;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa el perfil de un usuario, incluyendo su nivel de suscripción y estado de baneo.
    /// Esta información se almacena en el documento del usuario en Firestore.
    /// </summary>
    [FirestoreData]
    public class UserProfile
    {
        /// <summary>
        /// El correo del usuario
        /// </summary>
        [FirestoreProperty("email")]
        public string Email { get; set; } = string.Empty;
        /// <summary>
        /// El nombre de usuario
        /// </summary>
        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// El nivel de suscripción actual del usuario.
        /// </summary>
        [FirestoreProperty("subscription_tier")]
        public string Tier { get; set; } = SubscriptionTier.Free.ToString();

        /// <summary>
        /// Indica si el usuario tiene el acceso bloqueado a la aplicación.
        /// </summary>
        [FirestoreProperty("is_banned")]
        public bool IsBanned { get; set; } = false;
    }
}