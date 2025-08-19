using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Representa las estadísticas de uso de un usuario para diferentes características.
    /// Esta estructura se almacena dentro del documento del usuario en Firestore.
    /// </summary>
    public class UsageStats
    {
        /// <summary>
        /// Almacena el recuento diario de uso por característica.
        /// La clave es el nombre de la característica (ej. "QuickMode") y el valor es el recuento.
        /// </summary>
        [JsonPropertyName("daily_counts")]
        public Dictionary<string, int> DailyCounts { get; set; } = new();

        /// <summary>
        /// Almacena el recuento mensual de uso por característica.
        /// </summary>
        [JsonPropertyName("monthly_counts")]
        public Dictionary<string, int> MonthlyCounts { get; set; } = new();

        /// <summary>
        /// La fecha en que los recuentos diarios fueron reiniciados por última vez (en UTC).
        /// </summary>
        [JsonPropertyName("daily_reset_date")]
        public DateTime DailyResetDate { get; set; } = DateTime.UtcNow.Date;


        /// <summary>
        /// La fecha en que los recuentos mensuales fueron reiniciados por última vez (en UTC).
        /// </summary>
        [JsonPropertyName("monthly_reset_date")]
        public DateTime MonthlyResetDate { get; set; } = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}