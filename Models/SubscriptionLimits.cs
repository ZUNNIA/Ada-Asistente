using System.Collections.Generic;

namespace AsistenteVirtual.Models
{
    /// <summary>
    /// Define los límites de uso para cada nivel de suscripción.
    /// Es una clase estática para acceder a los límites de forma centralizada.
    /// </summary>
    public static class SubscriptionLimits
    {
        // Define los nombres de las características para consistencia.
        public const string MainMode = "Uso del Modo Principal";
        public const string QuickMode = "Uso de Modo Rápido";
        public const string ReasoningMode = "Uso de Modo Razonador";
        public const string WebSearch = "Búsqueda Web";

        // Mapeo de límites por nivel de suscripción.
        public static readonly Dictionary<SubscriptionTier, Dictionary<string, (int Daily, int Monthly)>> Limits = new()
        {
            [SubscriptionTier.Free] = new Dictionary<string, (int, int)>
            {
                { MainMode,      (Daily: 5, Monthly: 150) },
                { QuickMode,     (Daily: 5, Monthly: 150) },
                { ReasoningMode, (Daily: 2, Monthly: 60)  },
                { WebSearch,     (Daily: 2, Monthly: 60)  }
            },
            [SubscriptionTier.Esencial] = new Dictionary<string, (int, int)>
            {
                { MainMode,      (Daily: 30, Monthly: 900)  },
                { QuickMode,     (Daily: 70, Monthly: 2100) },
                { ReasoningMode, (Daily: 10, Monthly: 300)  },
                { WebSearch,     (Daily: 10, Monthly: 300)  }
            },
            [SubscriptionTier.Plus] = new Dictionary<string, (int, int)>
            {
                { MainMode,      (Daily: 90,  Monthly: 2700) },
                { QuickMode,     (Daily: 210, Monthly: 6300) },
                { ReasoningMode, (Daily: 30,  Monthly: 900)  },
                { WebSearch,     (Daily: 30,  Monthly: 900)  }
            },
            [SubscriptionTier.Premium] = new Dictionary<string, (int, int)>
            {
                { MainMode,      (Daily: 180, Monthly: 5400)  },
                { QuickMode,     (Daily: 420, Monthly: 12600) },
                { ReasoningMode, (Daily: 60,  Monthly: 6030)  },
                { WebSearch,     (Daily: 60,  Monthly: 6030)  }
            }
        };
    }
}