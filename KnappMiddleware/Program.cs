using KnappMiddleware;
using KnappMiddleware.Auditing;
using KnappMiddleware.Auth;
using KnappMiddleware.ErrorHandling;
using KnappMiddleware.Logging;
using KnappMiddleware.OpenApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

// Dos esquemas conviven:
//  - Cookie (default): panel administrativo (/admin, /api/v1/admin/*, /api/v1/configurations, etc.).
//    Login explícito vía AdminAuthController; cookie de sesión (IsPersistent = false) que el navegador
//    descarta al cerrarse — nunca sobrevive un reinicio del navegador.
//  - Basic (spec sección 10): canal SAP-facing exclusivamente. Los controllers bajo Controllers/Sap y
//    SftpFileController fijan AuthenticationSchemes = BasicAuthenticationHandler.SchemeName explícito
//    para no heredar el default Cookie.
// /health, /scalar, /openapi y / quedan excluidos vía AllowAnonymous (documentación de solo lectura).
// Por defecto todo endpoint requiere rol SuperUsuario; los endpoints SAP-facing (p. ej. /sftp-file) usan
// la policy SapOrSuperUsuario para aceptar también el rol Sap.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "km_admin_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
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

// Gate estilo cPanel: sin cookie de sesión válida (rol SuperUsuario), el shell del panel
// (index.html) ni siquiera se entrega — se redirige a login.html antes de tocar UseStaticFiles.
// login.html/.css/.js y admin.css quedan públicos (sin datos) para que el login pueda cargar.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.Equals("/admin", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/admin/index.html", StringComparison.OrdinalIgnoreCase))
    {
        var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || !result.Principal!.IsInRole(nameof(UserRole.SuperUsuario)))
        {
            context.Response.Redirect("/admin/login.html");
            return;
        }
    }
    await next();
});

app.UseStaticFiles();

app.UseAuthentication();

app.MapGet("/admin", () => Results.Redirect("/admin/index.html", permanent: false)).AllowAnonymous();
app.UseAuthorization();

app.MapControllers();

app.Run();
