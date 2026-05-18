using System;
using System.IO;
using System.Text.Json;
using Vosk;
using Serilog;
using PaStream = PortAudioSharp.Stream;
using PortAudioSharp;
using System.Runtime.InteropServices;
using AsistenteVirtual.Services.Interfaces;

namespace AsistenteVirtual.Services.Implementations
{
    /// <summary>
    /// Implementación avanzada del motor de reconocimiento de voz (STT) fuera de línea mediante Vosk y PortAudio.
    /// </summary>
    /// <remarks>
    /// Este servicio gestiona el ciclo de vida de un flujo de audio en tiempo real, procesando búferes binarios 
    /// y transformándolos en texto mediante modelos neuronales locales. Utiliza interoperabilidad nativa (P/Invoke) 
    /// para acceder al hardware de sonido.
    /// </remarks>
    public class VoskVoiceRecognitionService : IVoiceRecognitionService, IDisposable
    {
        private readonly VoskRecognizer? _recognizer;
        private readonly Model? _model;
        private readonly PaStream? _audioStream;
        private readonly PaStream.Callback _streamCallback;

        /// <summary> 
        /// Se dispara cuando se detecta una frase parcial mientras el usuario aún habla.
        /// </summary>
        public event Action<string>? OnPartialResult;

        /// <summary> 
        /// Se dispara cuando el motor confirma una frase completa tras una pausa.
        /// </summary>
        public event Action<string>? OnFinalResult;

        /// <summary>
        /// Inicializa el motor de audio y carga el modelo de lenguaje desde el sistema de archivos.
        /// </summary>
        /// <remarks>
        /// Configura PortAudio para capturar audio en mono a 16kHz, que es el formato óptimo para el modelo Vosk.
        /// </remarks>
        /// <exception cref="DirectoryNotFoundException">Lanzada si el modelo de voz no existe en la ruta de recursos.</exception>
        public VoskVoiceRecognitionService()
        {
            _streamCallback = AudioCallback;

            try
            {
                Vosk.Vosk.SetLogLevel(-1); // Desactiva logs ruidosos de la librería nativa.

                string modelPath = Path.Combine(AppContext.BaseDirectory, "Resources/VoskModel");

                if (!Directory.Exists(modelPath))
                {
                    Log.Fatal("[Vosk] Modelo no encontrado en: {Path}", modelPath);
                    return;
                }

                _model = new Model(modelPath);
                _recognizer = new VoskRecognizer(_model, 16000.0f);
                _recognizer.SetMaxAlternatives(0);
                _recognizer.SetWords(false);

                PortAudio.Initialize();

                // Parámetros de entrada de hardware
                StreamParameters inputParams = new()
                {
                    device = PortAudio.DefaultInputDevice,
                    channelCount = 1,
                    sampleFormat = SampleFormat.Int16,
                    suggestedLatency = PortAudio.GetDeviceInfo(PortAudio.DefaultInputDevice).defaultLowInputLatency
                };

                _audioStream = new PaStream(
                    inputParams,
                    null,
                    16000,
                    256,
                    StreamFlags.ClipOff,
                    _streamCallback,
                    IntPtr.Zero
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Vosk] Error crítico durante la inicialización del hardware de audio.");
            }
        }

        /// <summary>
        /// Callback de bajo nivel invocado por PortAudio cuando hay nuevos datos en el búfer del micrófono.
        /// </summary>
        /// <param name="inputBuffer">Puntero a los datos de audio crudos.</param>
        /// <param name="frameCount">Número de cuadros de audio recibidos.</param>
        /// <returns>Resultado del callback para continuar o abortar el stream.</returns>
        private StreamCallbackResult AudioCallback(IntPtr inputBuffer, IntPtr outputBuffer, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
        {
            if (_recognizer == null) { return StreamCallbackResult.Abort; }

            try
            {
                int byteCount = (int)frameCount * 2; // Int16 = 2 bytes por cuadro
                byte[] buffer = new byte[byteCount];
                Marshal.Copy(inputBuffer, buffer, 0, byteCount);

                // Si el reconocedor acepta el búfer y detecta un final de frase
                if (_recognizer.AcceptWaveform(buffer, byteCount))
                {
                    ProcessResult(_recognizer.Result());
                }
                else
                {
                    ProcessResult(_recognizer.PartialResult());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Vosk] Error procesando búfer de audio.");
                return StreamCallbackResult.Abort;
            }

            return StreamCallbackResult.Continue;
        }

        /// <summary>
        /// Activa la captura del micrófono y comienza el procesamiento de voz.
        /// </summary>
        public void StartListening()
        {
            if (_audioStream == null) { return; }
            try
            {
                _audioStream.Start();
                Log.Information("[Vosk] Escucha activada.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Vosk] No se pudo iniciar el stream de audio.");
            }
        }

        /// <summary>
        /// Detiene la captura del micrófono y libera el dispositivo de audio.
        /// </summary>
        public void StopListening()
        {
            if (_audioStream == null) { return; }
            try
            {
                if (!_audioStream.IsStopped) { _audioStream.Stop(); }
                Log.Information("[Vosk] Escucha desactivada.");

                if (_recognizer != null)
                {
                    ProcessResult(_recognizer.FinalResult());
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[Vosk] Advertencia al detener el dispositivo: {Msg}", ex.Message);
            }
        }

        /// <summary>
        /// Deserializa la respuesta JSON del motor Vosk y notifica a los suscriptores.
        /// </summary>
        /// <param name="jsonResult">Cadena JSON devuelta por el motor nativo.</param>
        private void ProcessResult(string jsonResult)
        {
            if (string.IsNullOrWhiteSpace(jsonResult)) { return; }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonResult);
                if (doc.RootElement.TryGetProperty("text", out JsonElement finalText))
                {
                    string? text = finalText.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) { OnFinalResult?.Invoke(text); }
                }
                else if (doc.RootElement.TryGetProperty("partial", out JsonElement partialText))
                {
                    string? text = partialText.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) { OnPartialResult?.Invoke(text); }
                }
            }
            catch (JsonException) { /* Ignorar JSON malformado del motor */ }
        }

        /// <summary>
        /// Libera todos los recursos no administrados, cierra el stream de audio y descarga el modelo de memoria.
        /// </summary>
        public void Dispose()
        {
            try { if (_audioStream != null && !_audioStream.IsStopped) { _audioStream.Stop(); } } catch { }
            _audioStream?.Dispose();
            try { PortAudio.Terminate(); } catch { }
            _recognizer?.Dispose();
            _model?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}