using KnappMiddleware.Auditing;
using KnappMiddleware.Auth;
using KnappMiddleware.Configuration;
using KnappMiddleware.Eventos;
using KnappMiddleware.Matrix;
using KnappMiddleware.Postgres;
using KnappMiddleware.RabbitMq;
using KnappMiddleware.Sap;
using KnappMiddleware.Sftp;
using KnappMiddleware.Tcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KnappMiddleware;

/// <summary>
/// Punto único de registro de dependencias del núcleo (Core) del middleware.
/// Los servicios en proceso (ClsXxx) se instancian con fábrica explícita; los adaptadores externos
/// (TCP, RabbitMQ, SFTP, Postgres) conservan sus interfaces. Mantén este método lo más plano posible.
///
/// Única configuración que vive en appsettings: la cadena de conexión de Postgres (y el logging de
/// arranque, Loki/FileLogging, en Program.cs) — todo lo demás (RabbitMq, SFTP, canales KiSoft, SAP
/// webhook) se lee en caliente desde la tabla `configuracion` vía <see cref="ClsConfigGate"/>.
/// </summary>
public static class WireUp
{
    public static IServiceCollection AddKnappCore(this IServiceCollection services, IConfiguration configuration)
    {
        // --- Configuración tipada desde appsettings: SOLO Postgres (todo lo demás vive en la tabla configuracion) ---
        services.AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .ValidateOnStart();

        // --- Adaptadores externos (conservan interfaz) ---
        // RabbitMq y SFTP se resuelven de forma perezosa (primer publish/consume/sftp real, ya con
        // ConfigGate cargado), así que basta con leer configuracion una sola vez al construirlos.
        services.AddSingleton<IOptions<RabbitMqOptions>>(sp =>
            Options.Create(RabbitMqOptions.ReadFrom(sp.GetRequiredService<ClsConfigGate>())));
        services.AddSingleton<IOptions<InventorySftpOptions>>(sp =>
            Options.Create(InventorySftpOptions.ReadFrom(sp.GetRequiredService<ClsConfigGate>())));
        services.AddSingleton<IOptions<PrintSftpOptions>>(sp =>
            Options.Create(PrintSftpOptions.ReadFrom(sp.GetRequiredService<ClsConfigGate>())));

        services.AddSingleton<RabbitMqConnectionManager>();
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddSingleton<IRabbitMqConsumer, RabbitMqConsumer>();
        services.AddSingleton<IRabbitMqQueueMonitor, RabbitMqQueueMonitor>();

        services.AddSingleton<IInventorySftpService, InventorySftpService>();
        services.AddSingleton<IPrintSftpService, PrintSftpService>();

        // Los canales KiSoft leen configuracion en cada intento de conexión (no al construirse): se
        // construyen como dependencia de ServicioCanalesKiSoft, y TODOS los IHostedService se
        // instancian en un mismo lote antes de que corra el StartAsync de ConfigGateStartupService.
        services.AddSingleton<ICanalEventoKiSoft, CanalEventoKiSoft>();
        services.AddSingleton<ICanalPedidoKiSoft, CanalPedidoKiSoft>();

        // Sin URL base fija: ClienteWebhookSap arma la URL completa en cada llamada a partir de
        // configuracion (tabla Postgres), para poder cambiarla en caliente sin reiniciar la Api.
        services.AddHttpClient<IClienteWebhookSap, ClienteWebhookSap>();

        // --- Repositorios Postgres (interfaz conservada por test seam) ---
        services.AddSingleton(sp =>
            NpgsqlDataSource.Create(sp.GetRequiredService<IOptions<PostgresOptions>>().Value.ConnectionString));
        services.AddSingleton<IMatrixRepository, PostgresMatrixRepository>();
        services.AddSingleton<IUserRepository, PostgresUserRepository>();
        services.AddSingleton<IConfigRepository, PostgresConfigRepository>();
        services.AddSingleton<IAuditReader, PostgresAuditReader>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // --- Servicios en proceso (ClsXxx) - instanciación con fábrica ---
        services.AddSingleton(sp => new ClsConfigGate(sp.GetRequiredService<IConfigRepository>()));
        services.AddSingleton(sp => new ClsMatrixGate(sp.GetRequiredService<IMatrixRepository>()));
        services.AddSingleton(sp => new ClsUserGate(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<IPasswordHasher>()));
        services.AddSingleton(sp => new ClsAuditToggle(sp.GetRequiredService<ClsConfigGate>(), sp.GetRequiredService<IConfigRepository>()));
        services.AddSingleton(sp => new ClsAuditWriter(sp.GetRequiredService<ClsAuditToggle>(), sp.GetRequiredService<ClsConfigGate>()));

        // --- Hosted services ---
        // Orden importa: los gates (Config/Matrix/User) deben cargar su snapshot ANTES de que arranque
        // nada que dependa de configuracion en caliente (canales KiSoft, dispatcher de eventos). El
        // generic host espera cada StartAsync en orden de registro antes de llamar al siguiente.
        services.AddHostedService<ConfigGateStartupService>();
        services.AddHostedService<MatrixGateStartupService>();
        services.AddHostedService<UserGateStartupService>();
        services.AddHostedService<DespachadorEventoPedidoKiSoft>();
        services.AddHostedService<ServicioCanalesKiSoft>();
        services.AddHostedService<AuditWriterBackgroundService>();

        return services;
    }
}
