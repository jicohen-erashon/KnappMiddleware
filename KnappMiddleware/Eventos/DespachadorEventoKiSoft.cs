using KnappMiddleware.Tcp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Se suscribe a <see cref="ICanalEventoKiSoft.TelegramReceived"/> y enruta cada trama al primer
/// <see cref="IManejadorEventoKiSoft"/> registrado que la reconozca por su identificador de registro.
/// Si ninguno la reconoce, acusa con el formato de "identificador de registro no válido" (HIS §2.5).
/// </summary>
public sealed class DespachadorEventoKiSoft : IHostedService
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly IEnumerable<IManejadorEventoKiSoft> _manejadores;
    private readonly ILogger<DespachadorEventoKiSoft> _logger;

    public DespachadorEventoKiSoft(
        ICanalEventoKiSoft eventChannel, IEnumerable<IManejadorEventoKiSoft> manejadores, ILogger<DespachadorEventoKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _manejadores = manejadores;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _eventChannel.TelegramReceived += HandleAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _eventChannel.TelegramReceived -= HandleAsync;
        return Task.CompletedTask;
    }

    private async Task HandleAsync(string data, CancellationToken cancellationToken)
    {
        var manejador = _manejadores.FirstOrDefault(m => m.PuedeManejar(data));
        if (manejador is null)
        {
            await AcknowledgeUnknownAsync(data, cancellationToken);
            return;
        }

        await manejador.ManejarAsync(data, cancellationToken);
    }

    /// <summary>HIS §2.5: identificador de registro no válido → "2" + 2º/3er dígito del recibido + estado "91".</summary>
    private async Task AcknowledgeUnknownAsync(string data, CancellationToken cancellationToken)
    {
        var recordId = data.Length >= 3 ? data[..3] : "000";
        var ack = recordId.Length == 3 ? $"2{recordId[1..3]}91" : "00091";
        _logger.LogWarning("Evento KiSoft con identificador de registro no soportado: '{RecordId}'.", recordId);
        await _eventChannel.AcknowledgeAsync(ack, cancellationToken);
    }
}
