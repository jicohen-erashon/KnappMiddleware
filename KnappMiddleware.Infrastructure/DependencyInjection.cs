using KnappMiddleware.Domain.Auditing;
using KnappMiddleware.Domain.Auth;
using KnappMiddleware.Domain.Configuration;
using KnappMiddleware.Domain.Matrix;
using KnappMiddleware.Infrastructure.Auditing;
using KnappMiddleware.Infrastructure.Auth;
using KnappMiddleware.Infrastructure.Configuration;
using KnappMiddleware.Infrastructure.Postgres;
using KnappMiddleware.Infrastructure.RabbitMq;
using KnappMiddleware.Infrastructure.Sftp;
using KnappMiddleware.Infrastructure.Tcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KnappMiddleware.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddKnappInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<InventorySftpOptions>()
            .Bind(configuration.GetSection(InventorySftpOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<PrintSftpOptions>()
            .Bind(configuration.GetSection(PrintSftpOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<KiSoftEventChannelOptions>()
            .Bind(configuration.GetSection(KiSoftEventChannelOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<KiSoftOrderChannelOptions>()
            .Bind(configuration.GetSection(KiSoftOrderChannelOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnectionManager>();
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddSingleton<IRabbitMqConsumer, RabbitMqConsumer>();
        services.AddSingleton<IRabbitMqQueueMonitor, RabbitMqQueueMonitor>();

        services.AddSingleton<IInventorySftpService, InventorySftpService>();
        services.AddSingleton<IPrintSftpService, PrintSftpService>();

        services.AddSingleton<IKiSoftEventChannel, KiSoftEventChannel>();
        services.AddSingleton<IKiSoftOrderChannel, KiSoftOrderChannel>();
        services.AddHostedService<KiSoftChannelsHostedService>();

        services.AddSingleton(sp =>
            NpgsqlDataSource.Create(sp.GetRequiredService<IOptions<PostgresOptions>>().Value.ConnectionString));

        services.AddSingleton<IMatrixRepository, PostgresMatrixRepository>();
        services.AddSingleton<IMatrixGate, MatrixGate>();
        services.AddHostedService<MatrixGateStartupService>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IUserRepository, PostgresUserRepository>();
        services.AddSingleton<IUserGate, UserGate>();
        services.AddHostedService<UserGateStartupService>();

        services.AddSingleton<IConfigRepository, PostgresConfigRepository>();
        services.AddSingleton<IConfigGate, ConfigGate>();
        services.AddHostedService<ConfigGateStartupService>();

        services.AddSingleton<IAuditToggle, AuditToggle>();
        services.AddSingleton<AuditWriter>();
        services.AddSingleton<IAuditWriter>(sp => sp.GetRequiredService<AuditWriter>());
        services.AddSingleton<IAuditReader, PostgresAuditReader>();
        services.AddHostedService<AuditWriterBackgroundService>();

        return services;
    }
}
