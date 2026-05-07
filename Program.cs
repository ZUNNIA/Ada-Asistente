using Avalonia;
using Serilog;
using Serilog.Events;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace AsistenteVirtual
{
    /// <summary>
    /// Punto de entrada estático para la ejecución del proceso de la aplicación.
    /// </summary>
    internal sealed class Program
    {
        /// <summary>
        /// Método principal que arranca la aplicación.
        /// </summary>
        /// <param name="args">Argumentos de línea de comandos.</param>
        [STAThread]
        public static void Main(string[] args)
        {
            ConfigureLogging();

            try
            {
                Log.Information("--------------------------------------------------");
                Log.Information("Arrancando Ada Asistente. Versión: {Version}",
                    Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Desconocida");
                Log.Information("--------------------------------------------------");

                _ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "La aplicación terminó inesperadamente debido a un error crítico.");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Configura el motor de logging Serilog con escritura en consola y archivo rotativo.
        /// </summary>
        private static void ConfigureLogging()
        {
#if DEBUG
            const LogEventLevel minLevel = LogEventLevel.Debug;
#else
            const LogEventLevel minLevel = LogEventLevel.Information;
#endif
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ada", "Logs", "log-.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(minLevel)
                .Enrich.FromLogContext()
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.File(logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture)
                .CreateLogger();
        }

        /// <summary>
        /// Configura el constructor de la aplicación Avalonia.
        /// </summary>
        /// <returns>Una instancia configurada de <see cref="AppBuilder"/>.</returns>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                        .UsePlatformDetect()
                        .WithInterFont()
                        .LogToTrace();
        }
    }
}