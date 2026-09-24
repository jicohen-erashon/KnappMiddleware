using System.Text;

namespace KnappMiddleware.Telegramas;

/// <summary>
/// Codifica en hexadecimal la trama de red COMPLETA (con los delimitadores &lt;LF&gt;/&lt;CR&gt; y el
/// campo de longitud — ver <see cref="CodecTramaTelegrama"/>), para poder guardarla en
/// buzon_entrada/buzon_salida sin que los bytes de control (0x0A/0x0D) rompan la lectura o
/// visualización de una columna TEXT. Uso: auditoría/trazabilidad únicamente, no forma parte del
/// protocolo real con KiSoft.
/// </summary>
public static class TramaHex
{
    /// <summary>Codifica el cuerpo de un telegrama (sin envolver) a la trama de red completa, en hex.</summary>
    public static string Encode(string innerData) =>
        Convert.ToHexString(Encoding.ASCII.GetBytes(CodecTramaTelegrama.Encode(innerData)));
}
