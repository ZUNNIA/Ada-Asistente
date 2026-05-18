using AsistenteVirtual.Commands;
using AsistenteVirtual.Constants;
using AsistenteVirtual.Services.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AsistenteVirtual.ViewModels
{
    /// <summary>
    /// ViewModel encargado de gestionar la lógica de la tienda, selección de productos y pasarela de pago.
    /// </summary>
    /// <remarks>
    /// Centraliza el mapeo de productos técnicos del backend con sus representaciones visuales y gestiona 
    /// la internacionalización de las características de los planes.
    /// </remarks>
    public class StoreViewModel : ViewModelBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IAuthService _authService;
        private readonly INotificationService _notificationService;

        private static readonly Dictionary<string, string> s_productTypeMap = new()
        {
            { "Escencial", "subscription" }, { "Plus", "subscription" }, { "Pro", "subscription" },
            { "Básico", "generation_pack" }, { "Popular", "generation_pack" }, { "Creador", "generation_pack" }, { "Estudio", "generation_pack" },
            { "Impulso", "chat_top_up" }, { "Supercarga", "chat_top_up" }
        };

        private string _selectedStoreTab = TabNames.StoreSubscriptions;

        /// <summary>
        /// Obtiene o establece la pestaña activa de la tienda (Suscripciones o Paquetes).
        /// </summary>
        public string SelectedStoreTab
        {
            get => _selectedStoreTab;
            set
            {
                if (SetProperty(ref _selectedStoreTab, value))
                {
                    OnPropertyChanged(nameof(IsSubscriptionsTabActive));
                    OnPropertyChanged(nameof(IsPackagesTabActive));
                }
            }
        }

        /// Indica si la pestaña de suscripciones mensuales es la que se muestra.
        public bool IsSubscriptionsTabActive => SelectedStoreTab == TabNames.StoreSubscriptions;

        /// Indica si la pestaña de recarga de créditos (Top-up) es la que se muestra.
        public bool IsPackagesTabActive => SelectedStoreTab == TabNames.StorePackages;

        /// Lista de beneficios del plan Escencial localizada.
        public ObservableCollection<string> EscencialPlanFeatures { get; } = [];

        /// Lista de beneficios del plan Plus localizada.
        public ObservableCollection<string> PlusPlanFeatures { get; } = [];

        /// Lista de beneficios del plan Pro localizada.
        public ObservableCollection<string> ProPlanFeatures { get; } = [];

        /// Comando para iniciar el proceso de compra de un producto específico.
        public ICommand PurchaseProductCommand { get; }

        /// Comando para alternar entre las categorías de la tienda.
        public ICommand SelectStoreTabCommand { get; }

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="StoreViewModel"/>.
        /// </summary>
        /// <param name="paymentService">Servicio para crear sesiones de pago.</param>
        /// <param name="authService">Servicio para validar la identidad del comprador.</param>
        /// <param name="notificationService">Servicio para informar de errores en el proceso de pago.</param>
        public StoreViewModel(IPaymentService paymentService, IAuthService authService, INotificationService notificationService)
        {
            _paymentService = paymentService;
            _authService = authService;
            _notificationService = notificationService;

            PurchaseProductCommand = new RelayCommand(async (param) => await PurchaseProductAsync(param as string));
            SelectStoreTabCommand = new RelayCommand(tab => { if (tab is string t) { SelectedStoreTab = t; } return Task.CompletedTask; });

            RefreshTexts();
        }

        /// <summary>
        /// Actualiza dinámicamente las listas de características de los planes cuando cambia el idioma de la aplicación.
        /// </summary>
        public void RefreshTexts()
        {
            string creditsLabel = App.GetString("Store_Credits");
            string accumulable = App.GetString("Feat_Accumulable");

            EscencialPlanFeatures.Clear();
            EscencialPlanFeatures.Add($"40,000 {creditsLabel}/mes");
            EscencialPlanFeatures.Add(accumulable);

            PlusPlanFeatures.Clear();
            PlusPlanFeatures.Add($"105,000 {creditsLabel}/mes");
            PlusPlanFeatures.Add(accumulable);

            ProPlanFeatures.Clear();
            ProPlanFeatures.Add($"180,000 {creditsLabel}/mes");
            ProPlanFeatures.Add(accumulable);
        }

        /// <summary>
        /// Orquesta la petición de pago al backend y redirige al usuario a la URL de Stripe.
        /// </summary>
        /// <param name="productId">Identificador comercial del producto.</param>
        /// <returns>Tarea que representa la operación de checkout.</returns>
        private async Task PurchaseProductAsync(string? productId)
        {
            if (string.IsNullOrEmpty(productId) || _authService.CurrentUser == null) { return; }

            if (!s_productTypeMap.TryGetValue(productId, out string? productType))
            {
                _notificationService.ShowTemporaryNotification(App.GetString("Msg_ProdUnknown"), true);
                return;
            }

            string? userToken = await _authService.GetCurrentUserTokenAsync();
            if (string.IsNullOrEmpty(userToken))
            {
                _notificationService.ShowTemporaryNotification(App.GetString("Msg_LoginReq"), true);
                return;
            }

            try
            {
                string checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(userToken, productId, productType);
                _ = Process.Start(new ProcessStartInfo(checkoutUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[StoreVM] Error iniciando sesión de pago para {ProductId}", productId);
                _notificationService.ShowTemporaryNotification(App.GetString("Msg_PayError"), true);
            }
        }
    }
}