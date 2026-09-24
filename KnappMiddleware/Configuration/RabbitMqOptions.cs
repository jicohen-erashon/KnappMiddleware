namespace KnappMiddleware.Configuration;

public sealed class RabbitMqOptions
{
    public required string HostName { get; set; }
    public int Port { get; set; } = 5672;
    public required string UserName { get; set; }
    public required string Password { get; set; }
    public string VirtualHost { get; set; } = "/";
    public required string InboundExchange { get; set; }
    public required string InboundQueue { get; set; }
    public required string OutboundExchange { get; set; }
    public required string OutboundRoutingKey { get; set; }

    /// <summary>Lee la configuración de RabbitMq desde la tabla configuracion (no vive en appsettings).</summary>
    public static RabbitMqOptions ReadFrom(ClsConfigGate configGate) => new()
    {
        HostName = configGate.GetValue("rabbitmq.hostName") ?? throw MissingKey("rabbitmq.hostName"),
        Port = configGate.GetInt("rabbitmq.port", 5672),
        UserName = configGate.GetValue("rabbitmq.userName") ?? throw MissingKey("rabbitmq.userName"),
        Password = configGate.GetValue("rabbitmq.password") ?? throw MissingKey("rabbitmq.password"),
        VirtualHost = configGate.GetValue("rabbitmq.virtualHost") ?? "/",
        InboundExchange = configGate.GetValue("rabbitmq.inboundExchange") ?? throw MissingKey("rabbitmq.inboundExchange"),
        InboundQueue = configGate.GetValue("rabbitmq.inboundQueue") ?? throw MissingKey("rabbitmq.inboundQueue"),
        OutboundExchange = configGate.GetValue("rabbitmq.outboundExchange") ?? throw MissingKey("rabbitmq.outboundExchange"),
        OutboundRoutingKey = configGate.GetValue("rabbitmq.outboundRoutingKey") ?? throw MissingKey("rabbitmq.outboundRoutingKey")
    };

    private static InvalidOperationException MissingKey(string clave) =>
        new($"Falta la clave '{clave}' en la tabla configuracion.");
}
