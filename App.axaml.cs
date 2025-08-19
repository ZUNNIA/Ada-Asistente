using AsistenteVirtual.Services;
using AsistenteVirtual.Services.Implementations;
using AsistenteVirtual.ViewModels;
using AsistenteVirtual.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;

namespace AsistenteVirtual
{
    /// <summary>
    /// Clase principal de la aplicación Avalonia. Gestiona el ciclo de vida
    /// y la configuración de la inyección de dependencias.
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        /// <summary>
        /// Se llama cuando Avalonia ha terminado de inicializar. Es el punto de partida de la lógica.
        /// </summary>
        public override void OnFrameworkInitializationCompleted()
        {
            // Construye la "caja de herramientas" de servicios.
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Crear y mostrar la ventana principal.
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                mainWindow.DataContext = mainViewModel;
                desktop.MainWindow = mainWindow;
            }
            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Configura la inyección de dependencias para la aplicación.
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // --- REGISTRO DE SERILOG EN EL CONTENEDOR DE DI ---
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: false);
            });
            // --- CONFIGURACIÓN DE PROTECCIÓN DE DATOS ---
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string keysPath = Path.Combine(appDataPath, "Ada", "ProtectionKeys");
            
            services.AddDataProtection()
                .SetApplicationName("Ada")
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

            // --- SERVICIOS DEL CLIENTE ---
            services.AddSingleton<IAuthService, GoogleAuthService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton(Dispatcher.UIThread);
            services.AddSingleton<IStorageService, MainWindow>();

            // --- SERVICIO DE BACKEND ---
            services.AddSingleton<IBackendService, BackendService>();

            // --- REGISTRO DE VIEWMODELS Y VISTAS ---
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainWindow>();
        }
    }
}