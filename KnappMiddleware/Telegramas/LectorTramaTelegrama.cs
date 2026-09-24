using System.Text;

namespace KnappMiddleware.Telegramas;

/// <summary>
/// Reensambla tramas completas (&lt;LF&gt; + longitud + datos + &lt;CR&gt;) a partir de fragmentos
/// de texto recibidos incrementalmente desde un socket TCP. No realiza I/O: solo mantiene el
/// estado del buffer entre llamadas a <see cref="Feed"/>.
/// </summary>
public sealed class LectorTramaTelegrama
{
    private readonly StringBuilder _buffer = new();

    /// <summary>
    /// Añade un fragmento recién leído del socket y devuelve los datos (sin delimitadores)
    /// de cada trama que haya quedado completa con este fragmento, en orden de llegada.
    /// </summary>
    public IReadOnlyList<string> Feed(string chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        _buffer.Append(chunk);
        var frames = new List<string>();

        while (TryExtractFrame(out var data))
        {
            frames.Add(data);
        }

        return frames;
    }

    /// <summary>Descarta cualquier fragmento de trama incompleto acumulado (usar al reconectar).</summary>
    public void Reset() => _buffer.Clear();

    private bool TryExtractFrame(out string data)
    {
        data = string.Empty;

        var text = _buffer.ToString();
        var startIndex = text.IndexOf(CodecTramaTelegrama.StartDelimiter);
        if (startIndex < 0)
        {
            _buffer.Clear();
            return false;
        }

        if (startIndex > 0)
        {
            text = text[startIndex..];
        }

        if (text.Length < CodecTramaTelegrama.LengthFieldWidth + 1)
        {
            _buffer.Clear();
            _buffer.Append(text);
            return false;
        }

        var lengthField = text.Substring(1, CodecTramaTelegrama.LengthFieldWidth);
        if (!int.TryParse(lengthField, out var totalLength))
        {
            _buffer.Clear();
            throw new ExcepcionFormatoTelegrama($"Campo de longitud inválido: '{lengthField}'.");
        }

        var frameRawLength = totalLength + 2;
        if (text.Length < frameRawLength)
        {
            _buffer.Clear();
            _buffer.Append(text);
            return false;
        }

        data = CodecTramaTelegrama.Decode(text[..frameRawLength]);

        _buffer.Clear();
        _buffer.Append(text[frameRawLength..]);
        return true;
    }
}
