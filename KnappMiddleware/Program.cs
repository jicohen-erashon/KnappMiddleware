using KnappMiddleware;
using KnappMiddleware.Auditing;
using KnappMiddleware.Auth;
using KnappMiddleware.ErrorHandling;
using KnappMiddleware.Logging;
using KnappMiddleware.OpenApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using NLog;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNLog();

var logFilePath = builder.Configuration.GetSection("FileLogging:BasePath").Value
    ?? Path.Combine(AppContext.BaseDirectory, "logs");
var lokiEndpoint = Environment.GetEnvironmentVariable("__GRAFANA_LOKI_URL__")
    ?? builder.Configuration.GetSection("Loki:Endpoint").Value
    ?? "http://loki:3100";
if (LogManager.Configuration is not null)
{
    LogManager.Configuration.Variables["logFilePath"] = logFilePath;
    LogManager.Configuration.Variables["lokiEndpoint"] = lokiEndpoint;
    LogManager.Configuration.Variables["environment"] = builder.Environment.EnvironmentName;
    LogManager.ReconfigExistingLoggers();
}

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddKnappValidationAudit();
builder.Services.AddKnappErrorHandling();
builder.Services.AddKnappRequestResponseLogging(builder.Configuration);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddKnappOpenApi();
builder.Services.AddKnappCore(builder.Configuration);

// HTTP Basic sobre todo el canal SAP-facing (spec sección 10); /health, /scalar, /openapi y / quedan
// excluidos vía AllowAnonymous (documentación de solo lectura, sin exponer datos).
// Por defecto todo endpoint requiere rol SuperUsuario; los endpoints SAP-facing (p. ej. /sftp-file) usan
// la policy SapOrSuperUsuario para aceptar también el rol Sap.
builder.Services.AddAuthentication(BasicAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationHandler.SchemeName, options => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(nameof(UserRole.SuperUsuario))
        .Build();
    options.AddPolicy(AuthorizationPolicies.SapOrSuperUsuario, policy =>
        policy.RequireRole(nameof(UserRole.SuperUsuario), nameof(UserRole.Sap)));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Primero de todos: envuelve el pipeline completo para poder registrar la respuesta final (incluida la
// que produce el exception handler) en KnappMiddleware/Logging.
app.UseKnappRequestResponseLogging();

// Envuelve todo lo que sigue: solo actúa cuando un endpoint deja escapar una excepción sin
// capturarla (ver KnappMiddleware/ErrorHandling).
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapKnappApiDocs();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
