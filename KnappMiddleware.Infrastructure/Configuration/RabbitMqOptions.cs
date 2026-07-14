namespace KnappMiddleware.Infrastructure.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public required string HostName { get; set; }
    public int Port { get; set; } = 5672;
    public required string UserName { get; set; }
    public required string Password { get; set; }
    public string VirtualHost { get; set; } = "/";
    public required string InboundExchange { get; set; }
    public required string InboundQueue { get; set; }
    public required string OutboundExchange { get; set; }
    public required string OutboundRoutingKey { get; set; }
}
