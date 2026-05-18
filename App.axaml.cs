using AsistenteVirtual.Services.Implementations;
using AsistenteVirtual.ViewModels;
using AsistenteVirtual.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Net.Http;
using System.IO;
using AsistenteVirtual.Services.Interfaces;
using Avalonia.Controls;
using System.ComponentModel;
using Avalonia.Svg.Skia;
using AsistenteVirtual.Converters;

namespace AsistenteVirtual
{
    /// <summary>
    /// Punto de entrada principal de la lógica de la aplicación Avalonia.
    /// </summary>
    /// <remarks>
    /// Esta clase orquesta la configuración del contenedor de Inyección de Dependencias (DI), 
    /// la gestión de la localización dinámica y el ciclo de vida de la ventana principal.
    /// </remarks>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        /// <summary>
        /// Realiza la carga de la definición XAML de la aplicación.
        /// </summary>
        public override void Initialize()
        {
            _ = TypeDescriptor.AddAttributes(typeof(SvgSource), new TypeConverterAttribute(typeof(StringToSvgSourceConverter)));
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Se ejecuta cuando el framework ha terminado la inicialización básica.
        /// Configura los servicios y establece la vista inicial.
        /// </summary>
        public override void OnFrameworkInitializationCompleted()
        {
            ServiceCollection services = new();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                MainViewModel mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

                mainWindow.DataContext = mainViewModel;
                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Cambia dinámicamente el diccionario de recursos de idioma de la aplicación.
        /// </summary>
        /// <param name="languageCode">Código del idioma (ej. "Es", "En").</param>
        public void SetLanguage(string languageCode)
        {
            Uri uri = new($"avares://Ada/Resources/Languages/{languageCode}.axaml");
            ResourceInclude newResources = new(uri) { Source = uri };

            if (Resources.MergedDictionaries.Count > 0)
            {
                Resources.MergedDictionaries[0] = newResources;
            }
        }

        /// <summary>
        /// Recupera una cadena localizada desde los recursos de la aplicación.
        /// </summary>
        /// <param name="key">La clave del recurso de texto.</param>
        /// <returns>El texto localizado o la clave entre corchetes si no se encuentra.</returns>
        public static string GetString(string key)
        {
            return Current!.TryGetResource(key, out object? res) && res is string s ? s : $"[{key}]";
        }

        /// <summary>
        /// Registra todos los servicios, ViewModels y Vistas en el contenedor de dependencias.
        /// </summary>
        /// <param name="services">La colección de servicios a configurar.</param>
        private static void ConfigureServices(IServiceCollection services)
        {
            // --- Infraestructura y Logging ---
            _ = services.AddLogging(builder => { _ = builder.ClearProviders(); _ = builder.AddSerilog(dispose: false); });

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _ = services.AddDataProtection()
                .SetApplicationName("Ada")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(appDataPath, "Ada", "ProtectionKeys")));

            _ = services.AddSingleton<HttpClient>();
            _ = services.AddSingleton(Dispatcher.UIThread);

            // --- Servicios de Identidad y UI ---
            _ = services.AddSingleton<IAuthService, GoogleAuthService>();
            _ = services.AddSingleton<INotificationService, NotificationService>();
            _ = services.AddSingleton<IStorageService, MainWindow>();
            _ = services.AddSingleton<IUIService, UIService>();
            _ = services.AddSingleton<IVoiceRecognitionService, VoskVoiceRecognitionService>();

            // --- Servicios de Backend (Implementación de múltiples interfaces) ---
            _ = services.AddSingleton<BackendService>();
            _ = services.AddSingleton<IConversationService>(sp => sp.GetRequiredService<BackendService>());
            _ = services.AddSingleton<IFileStorageService>(sp => sp.GetRequiredService<BackendService>());
            _ = services.AddSingleton<IUserService>(sp => sp.GetRequiredService<BackendService>());
            _ = services.AddSingleton<IPaymentService>(sp => sp.GetRequiredService<BackendService>());

            _ = services.AddSingleton<IChatService, ChatService>();

            // --- Registro de ViewModels y Vistas ---
            _ = services.AddSingleton<MainViewModel>();
            _ = services.AddSingleton<MainWindow>();
        }
    }
}