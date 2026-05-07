using System;

namespace AsistenteVirtual.Services.Interfaces
{
    /// <summary>
    /// Define el contrato para el motor de procesamiento de voz a texto (STT).
    /// </summary>
    /// <remarks>
    /// Las implementaciones deben gestionar el hardware de audio y la 
    /// transformación de señales en texto de forma asíncrona.
    /// </remarks>
    public interface IVoiceRecognitionService : IDisposable
    {
        /// Se dispara cuando se detecta texto parcial mientras el usuario habla.
        event Action<string>? OnPartialResult;

        /// Se dispara cuando se confirma una frase completa tras una pausa de silencio.
        event Action<string>? OnFinalResult;

        /// Activa el micrófono e inicia el procesamiento de audio.
        void StartListening();

        /// Detiene la captura de audio y libera el dispositivo.
        void StopListening();
    }
}