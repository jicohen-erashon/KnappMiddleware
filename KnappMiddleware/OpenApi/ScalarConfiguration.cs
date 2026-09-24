using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace KnappMiddleware.OpenApi;

/// <summary>
/// Documentación de solo lectura (Scalar + OpenAPI), excluida del FallbackPolicy vía AllowAnonymous.
/// Declara el security scheme Basic en el documento OpenAPI para que Scalar muestre su propio campo de
/// usuario/contraseña (persistido en localStorage) en vez de depender únicamente del popup nativo del
/// navegador al ejecutar un "Test Request" real.
/// </summary>
public static class ScalarConfiguration
{
    private const string BasicAuthSchemeName = "BasicAuth";

    // Refleja en el sidebar de Scalar la misma organización por dominio que Controllers/{Sap,Knapp}:
    // cada tag corresponde al [Tags(...)] declarado en el controller correspondiente.
    private static readonly (string Name, string[] Tags)[] TagGroups =
    [
        ("SAP", ["Order", "Inventory", "Article", "Route", "BusinessPartner"]),
        ("Knapp - Administración", ["Auth", "Audit", "Config", "Matrix", "Health", "Status"]),
        ("Knapp - Canales", ["Tcp", "Sftp", "Queue"])
    ];

    public static IServiceCollection AddKnappOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BasicAuthSchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "basic"
                };

                var basicAuthRequirement = new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BasicAuthSchemeName, document, null)] = []
                };

                #pragma warning disable CS8602 // Dictionary<string, IOpenApiPathItem>.Values nunca contiene null en runtime.
                var operations = document.Paths?.Values.SelectMany(path => path.Operations.Values)
                    ?? [];
                #pragma warning restore CS8602
                foreach (var operation in operations)
                {
                    operation.Security ??= [];
                    operation.Security.Add(basicAuthRequirement);
                }

                document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                document.Extensions["x-tagGroups"] = new JsonNodeExtension(new JsonArray(TagGroups
                    .Select(group => (JsonNode)new JsonObject
                    {
                        ["name"] = group.Name,
                        ["tags"] = new JsonArray(group.Tags.Select(tag => (JsonNode)tag).ToArray())
                    })
                    .ToArray()));

                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static void MapKnappApiDocs(this WebApplication app)
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options => options
            .DisableAgent()
            .DisableMcp()
            .DisableTelemetry()
            .AddPreferredSecuritySchemes(BasicAuthSchemeName)
            .AddHttpAuthentication(BasicAuthSchemeName, scheme => { })
            .EnablePersistentAuthentication()
        ).AllowAnonymous();
        app.MapGet("/", () => Results.Redirect("/scalar")).AllowAnonymous();
    }
}
