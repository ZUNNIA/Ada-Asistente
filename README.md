# Ada Asistente

![Logo de Ada Asistente](https://raw.githubusercontent.com/ZUNNIA/AsistenteVirtual/main/Resources/Icons/ada.png)

Ada Asistente es un asistente de IA de escritorio multiplataforma construido con Avalonia UI y .NET, diseñado para proveer una experiencia rica e interactiva y con precios mas accesibles.

# Características

- Multiplataforma: Funciona nativamente en Windows y distribuciones de Linux como EndeavourOS.  
- Interacción de Usuario Avanzada:
  - Entrada Multimodal: Envía mensajes con texto y archivos adjuntos.
  - Soporte de Archivos: Soporta imágenes para visión (".png", ".jpeg", ".gif") y una amplia gama de documentos para análisis (".pdf", ".docx", ".cs", ".py", etc.).
  - Arrastrar y Soltar: Arrastra y suelta archivos fácilmente sobre la aplicación para adjuntarlos.
- Capacidades de IA:
  - Múltiples Modos de IA: Cambia entre diferentes modos como Rápido, Razonamiento y un modo principal balanceado para usar el mejor modelo para cada tarea.
  - Búsqueda Web: El asistente puede realizar búsquedas en la web para responder preguntas con información actualizada.
- Seguro y Personal:
  - Autenticación: Inicio de sesión seguro con Google o con una cuenta dedicada (correo/contraseña).
  - Historial de Conversaciones: Todas tus conversaciones se guardan en tu cuenta.

# Tecnología Utilizada

- Framework: .NET / C#
- UI: Avalonia UI
- Servicios Backend: Google Cloud Run, Google Cloud Firestore
- Proveedor de IA: OpenAI

## Cómo Empezar

Para compilar y ejecutar este proyecto localmente, necesitarás:

1. El SDK de .NET (versión 8.0 o superior).
2. Un clon local de este repositorio.

# Clona el repositorio
git clone https://github.com/ZUNNIA/AsistenteVirtual.git

# Navega a la carpeta del proyecto
cd AsistenteVirtual/Avalonia

# Restaura las dependencias
dotnet restore

# Ejecuta la aplicación
dotnet run

## Contribuciones e Idioma

Los comentarios en el código fuente están escritos principalmente en español.
Agradecemos contribuciones de todo tipo, desde documentación hasta nuevas características.

---

¡Gracias por tu interés en Ada Asistente!
