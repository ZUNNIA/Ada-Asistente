using MessagePack;
using MessagePack.Formatters;
using System;
using System.Globalization;

namespace AsistenteVirtual.Formatters
{
    /// <summary>
    /// Formateador especializado para la serialización de objetos <see cref="DateTime"/> en formato de cadena ISO 8601.
    /// </summary>
    /// <remarks>
    /// Resuelve problemas de interoperabilidad entre C# y microservicios de Python en Cloud Run, 
    /// asegurando que las fechas se transmitan como texto estandarizado en lugar de formatos binarios nativos de .NET.
    /// </remarks>
    public class StringDateTimeFormatter : IMessagePackFormatter<DateTime>
    {
        /// <summary>
        /// Escribe un valor <see cref="DateTime"/> en el flujo de MessagePack como una cadena formateada.
        /// </summary>
        /// <param name="writer">El escritor de MessagePack subyacente.</param>
        /// <param name="value">La fecha y hora a serializar.</param>
        /// <param name="options">Opciones de serialización actuales.</param>
        /// <remarks>Utiliza el formato "o" (Round-trip) para preservar la precisión de milisegundos y la zona horaria.</remarks>
        public void Serialize(ref MessagePackWriter writer, DateTime value, MessagePackSerializerOptions options)
        {
            writer.Write(value.ToString("o", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Lee una cadena del flujo de MessagePack y la convierte nuevamente en un objeto <see cref="DateTime"/>.
        /// </summary>
        /// <param name="reader">El lector de MessagePack subyacente.</param>
        /// <param name="options">Opciones de deserialización actuales.</param>
        /// <returns>El objeto <see cref="DateTime"/> reconstruido. Devuelve <see cref="DateTime.MinValue"/> si el formato es inválido.</returns>
        public DateTime Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            string? dateString = reader.ReadString();

            return DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime result)
                ? result
                : DateTime.MinValue;
        }
    }
}