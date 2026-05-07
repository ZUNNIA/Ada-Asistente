namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Define los costos en "Créditos" para cada acción de IA.
    /// Esta es la única fuente de verdad para la monetización.
    /// 1 Crédito = $0.0001 USD (Tasa de costo interno)
    /// </summary>
    public static class CreditCosts
    {
        // --- CARTERA DE CHAT ---

        /// <summary>
        /// Costo para un mensaje rápido (Non-Thinking).
        /// </summary>
        public const long ChatFlashQuick = 12;

        /// <summary>
        /// Costo para un mensaje razonador (Thinking).
        /// </summary>
        public const long ChatFlashReasoning = 14;

        /// <summary>
        /// Costo para un mensaje con el modelo Pro.
        /// </summary>
        public const long ChatPro = 54;

        /// <summary>
        /// Costo para una búsqueda web fundamentada.
        /// </summary>
        public const long WebSearch = 350;

        // --- CARTERA DE GENERACIÓN ---

        /// <summary>
        /// Costo para generar 1 imagen.
        /// </summary>
        public const long ImageGeneration = 600;

        /// <summary>
        /// Costo para editar 1 imagen.
        /// Incluye costos de entrada y generación de salida.
        /// </summary>
        public const long ImageEditing = 400;
    }
}