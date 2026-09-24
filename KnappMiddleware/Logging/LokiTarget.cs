using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NLog;
using NLog.Common;
using NLog.Layouts;
using NLog.Targets;

namespace KnappMiddleware.Logging;

/// <summary>
/// Push HTTP de eventos NLog al endpoint Loki (/loki/api/v1/push). Sustituye al paquete
/// NLog.Targets.Loki community porque su 2.7.2 no registra el alias [Target] que NLog
/// necesita para resolver el tipo desde nlog.config. Implementacion minima: usa el
/// batching nativo de AsyncTaskTarget (TaskDelayMilliseconds + BatchSize). Si Loki
/// esta caido se pierden los eventos del batch en vuelo (aceptable para logs no
/// transaccionales).
/// </summary>
[Target("Loki")]
public sealed class LokiTarget : AsyncTaskTarget
{
    private readonly HttpClient _httpClient = new();

    public string Endpoint { get; set; } = string.Empty;
    public Layout AppLabel { get; set; } = "${app}";
    public Layout EnvLabel { get; set; } = "${env}";

    public LokiTarget()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    protected override async Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(Endpoint))
        {
            return;
        }

        var stream = new Dictionary<string, string>
        {
            ["app"] = AppLabel.Render(logEvent),
            ["env"] = EnvLabel.Render(logEvent),
            ["level"] = logEvent.Level.Name.ToUpperInvariant(),
            ["logger"] = logEvent.LoggerName
        };

        var value = new[]
        {
            (logEvent.TimeStamp.ToUniversalTime().Ticks * 100L).ToString(),
            Layout.Render(logEvent)
        };

        var payload = JsonSerializer.Serialize(new
        {
            streams = new[] { new { stream, values = new[] { value } } }
        });

        try
        {
            using var content = new StringContent(payload, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await _httpClient.PostAsync(
                $"{Endpoint.TrimEnd('/')}/loki/api/v1/push",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                InternalLogger.Error("Loki responded {0}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            InternalLogger.Error(ex, "Loki push failed to {0}", Endpoint);
        }
    }
}
