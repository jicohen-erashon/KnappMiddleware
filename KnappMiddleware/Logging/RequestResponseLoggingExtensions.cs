namespace KnappMiddleware.Logging;

public static class RequestResponseLoggingExtensions
{
    public static IServiceCollection AddKnappRequestResponseLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FileLoggingOptions>()
            .Bind(configuration.GetSection(FileLoggingOptions.SectionName))
            .ValidateOnStart();
        return services;
    }

    public static IApplicationBuilder UseKnappRequestResponseLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestResponseLoggingMiddleware>();
}
