namespace KnappMiddleware.ErrorHandling;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddKnappErrorHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
