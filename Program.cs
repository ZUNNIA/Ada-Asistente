using Avalonia;
using System;
using Serilog;

namespace AsistenteVirtual
{
    /// <summary>
    /// Clase principal de la aplicación.
    /// </summary>
    internal class Program
    {
        // El punto de entrada principal de la aplicación.
        [STAThread]
        public static void Main(string[] args)
        {
            // --- CONFIGURACIÓN DE SERILOG ---   
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDirectory = System.IO.Path.Combine(appDataPath, "Ada", "Logs");
            string logFilePath = System.IO.Path.Combine(logDirectory, "AsistenteLog_.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information() // Captura todos los niveles de severidad.
                .WriteTo.Console()    // Escribe en la consola estándar (muy útil en Linux).
                .WriteTo.File(logFilePath,
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: 7,
                              // Forza la escritura al disco periódicamente para no perder logs.
                              flushToDiskInterval: TimeSpan.FromSeconds(1), 
                              shared: true)
                .CreateLogger();
            
            // Se envuelve la ejecución de la app en un bloque try/catch/finally.
            try
            {
                Log.Information("================= INICIO DE LA APP =================");
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                // Captura cualquier error fatal que pueda ocurrir durante el arranque.
                Log.Fatal(ex, "La aplicación ha terminado de forma inesperada.");
            }
            finally
            {
                // Asegura que todos los logs en buffer se escriban en el archivo antes de cerrar.
                Log.CloseAndFlush();
            }
        }
        // Configura y construye la instancia de la aplicación Avalonia.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont();
    }
}
