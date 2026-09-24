using System.Text.Json;
using KnappMiddleware.Contratos.Sap;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace KnappMiddleware.Auditing;

/// <summary>
/// Cuando [ApiController] rechaza un request por ModelState inválido (p. ej. un [StringLength]
/// excedido) el rechazo ocurre ANTES de que el método del controller llegue a ejecutarse — el flujo
/// normal de auditoría (que vive dentro de cada acción, ver Controllers/Sap/*.cs) nunca corre, y el
/// request queda completamente invisible en buzon_entrada/buzon_salida. Este factory intercepta ese
/// caso específico para las rutas /api/v1/sap/* y registra ambos lados (entrada "Recibido" + salida
/// "Error" con el detalle de validación) antes de devolver la misma respuesta 400 de siempre.
/// </summary>
public static class AuditValidationConfiguration
{
    public static IServiceCollection AddKnappValidationAudit(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            var defaultFactory = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/api/v1/sap"))
                {
                    LogValidationFailure(context);
                }

                return defaultFactory(context);
            };
        });

        return services;
    }

    private static void LogValidationFailure(ActionContext context)
    {
        var httpContext = context.HttpContext;
        var auditWriter = httpContext.RequestServices.GetRequiredService<ClsAuditWriter>();
        var correlationId = Guid.NewGuid();
        var http = AuditHttpContext.From(httpContext);
        var tipoTelegrama = httpContext.Request.Path.Value?.TrimEnd('/').Split('/').LastOrDefault() ?? "?";

        // El binding de [FromBody] ya construyó el DTO (aunque inválido según DataAnnotations) antes
        // de que se evalúe el ModelState; ActionContext es en realidad un ActionExecutingContext en
        // este punto del pipeline, así que ActionArguments sigue siendo accesible. Se excluye
        // explícitamente CancellationToken (el otro parámetro de toda acción SAP): al ser struct
        // nunca es "null", así que sin este filtro un binding fallido (p. ej. "station" ausente, con
        // required) hace que se recoja el CancellationToken en vez de un dto=null — y CancellationToken
        // no es serializable a JSON (contiene un IntPtr interno), lo que revienta con 500.
        var dto = context is ActionExecutingContext executing
            ? executing.ActionArguments.Values.FirstOrDefault(v => v is not null and not CancellationToken)
            : null;
        var payload = dto is not null ? JsonSerializer.Serialize(dto) : null;
        var (idObjeto, creadoPorSap) = dto is ISobreTelegramaSap sobre ? (sobre.ObjectId, sobre.TeCreatedBy) : (null, null);

        var errores = string.Join("; ", context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors.Select(e => $"{kvp.Key}: {e.ErrorMessage}")));

        auditWriter.EnqueueEntrada(new AuditRecord(correlationId, tipoTelegrama, "SAP", "Middleware", AuditEstado.Recibido,
            payload, Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));

        auditWriter.EnqueueSalida(new AuditRecord(correlationId, tipoTelegrama, "Middleware", "SAP", AuditEstado.Error,
            payload, errores, HttpStatus: StatusCodes.Status400BadRequest,
            Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));
    }
}
