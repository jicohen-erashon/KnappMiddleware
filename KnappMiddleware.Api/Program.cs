using KnappMiddleware.Api.Auth;
using KnappMiddleware.Domain.Auth;
using KnappMiddleware.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddKnappInfrastructure(builder.Configuration);

// HTTP Basic sobre todo el canal SAP-facing (spec sección 10); /health queda excluido vía [AllowAnonymous].
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
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithClassicLayout());
    app.MapGet("/", () => Results.Redirect("/scalar"));
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
