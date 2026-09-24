// Stub TCP de KiSoft One para pruebas locales end-to-end (14N/16N y demás). NO es parte de la
// aplicación de producción — es una reimplementación mínima y autónoma del protocolo de trama
// externa (<LF> + longitud(5) + datos + <CR>, HIS §2.2/§2.3) que el middleware ya implementa en
// KnappMiddleware/Telegramas/CodecTramaTelegrama.cs. Acepta la conexión entrante (el middleware
// siempre es el cliente que conecta) y responde cada trama recibida con un ack "OK" (recordId con
// el primer dígito '1' cambiado por '2' + estado "00"), salvo que se fuerce un estado de rechazo
// para el ack de un registro de datos vía el segundo argumento.
//
// Uso: dotnet run --project herramientas/StubKiSoft -- [puerto] [estadoRegistroDatos]
//   puerto:               por defecto 9801 (canal de pedidos/datos maestros)
//   estadoRegistroDatos:  por defecto "00" (sin errores). Solo se aplica al ack del REGISTRO de
//                         datos (p. ej. "14N"->"24N", "16N"->"26N"), no al abrir/cerrar el bracket
//                         (que siempre confirma "00" para que el bracket complete y se pueda ver el
//                         rechazo específico del registro).

using System.Net;
using System.Net.Sockets;
using System.Text;

var port = args.Length > 0 ? int.Parse(args[0]) : 9801;
var estadoRegistroDatos = args.Length > 1 ? args[1] : "00";

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
Console.WriteLine($"[StubKiSoft] Escuchando en el puerto {port} (estado forzado para registros de datos: {estadoRegistroDatos}).");

while (true)
{
    using var client = await listener.AcceptTcpClientAsync();
    Console.WriteLine($"[StubKiSoft] Conexión aceptada desde {client.Client.RemoteEndPoint}.");
    try
    {
        await HandleConnectionAsync(client, estadoRegistroDatos);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[StubKiSoft] Conexión cerrada ({ex.GetType().Name}: {ex.Message}).");
    }
}

static async Task HandleConnectionAsync(TcpClient client, string estadoRegistroDatos)
{
    var stream = client.GetStream();
    var buffer = new byte[8192];
    var pending = new StringBuilder();

    while (true)
    {
        var bytesRead = await stream.ReadAsync(buffer);
        if (bytesRead == 0)
        {
            Console.WriteLine("[StubKiSoft] El middleware cerró la conexión.");
            return;
        }

        pending.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

        while (TryExtractFrame(pending, out var data))
        {
            Console.WriteLine($"[StubKiSoft] <- '{data}'");
            var ack = BuildAck(data, estadoRegistroDatos);
            if (ack is null)
            {
                continue;
            }

            var frame = EncodeFrame(ack);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(frame));
            await stream.FlushAsync();
            Console.WriteLine($"[StubKiSoft] -> '{ack}'");
        }
    }
}

static string? BuildAck(string data, string estadoRegistroDatos)
{
    if (data == "1HR")
    {
        return "2HR";
    }

    if (data == "3HR")
    {
        // El stub no inicia heartbeats propios en este flujo de prueba; si el middleware alguna
        // vez reenvía un 3HR no hay nada que contestar desde este lado (KiSoft sería quien envía
        // 3HR, no quien lo recibe).
        return null;
    }

    if (data.Length < 3 || data[0] != '1')
    {
        Console.WriteLine($"[StubKiSoft] Trama no reconocida, se ignora: '{data}'.");
        return null;
    }

    var recordId = data[..3];
    var ackId = "2" + recordId[1..3];

    // Bracket de apertura/cierre (identificador numérico, p. ej. "141"/"149"/"161"/"169"): siempre
    // "00" para que el bracket complete y se pueda aislar el rechazo en el registro de datos.
    // Registro de datos (identificador termina en letra, p. ej. "14N"/"16N"): aplica el estado
    // forzado por línea de comandos (por defecto "00").
    var esRegistroDeDatos = char.IsLetter(recordId[2]);
    var estado = esRegistroDeDatos ? estadoRegistroDatos : "00";

    return ackId + estado;
}

static string EncodeFrame(string data)
{
    var totalLength = 5 + data.Length;
    var lengthField = totalLength.ToString().PadLeft(5, '0');
    return "\n" + lengthField + data + "\r";
}

static bool TryExtractFrame(StringBuilder buffer, out string data)
{
    data = string.Empty;
    var text = buffer.ToString();

    var startIndex = text.IndexOf('\n');
    if (startIndex < 0)
    {
        buffer.Clear();
        return false;
    }

    if (startIndex > 0)
    {
        text = text[startIndex..];
    }

    const int lengthFieldWidth = 5;
    if (text.Length < lengthFieldWidth + 1)
    {
        buffer.Clear();
        buffer.Append(text);
        return false;
    }

    var lengthField = text.Substring(1, lengthFieldWidth);
    if (!int.TryParse(lengthField, out var totalLength))
    {
        buffer.Clear();
        Console.WriteLine($"[StubKiSoft] Campo de longitud inválido: '{lengthField}'.");
        return false;
    }

    var frameRawLength = totalLength + 2; // + <LF> + <CR>
    if (text.Length < frameRawLength)
    {
        buffer.Clear();
        buffer.Append(text);
        return false;
    }

    data = text.Substring(1 + lengthFieldWidth, totalLength - lengthFieldWidth);

    buffer.Clear();
    buffer.Append(text[frameRawLength..]);
    return true;
}
