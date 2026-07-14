using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using KnappMiddleware.Domain.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Api.Auth;

/// <summary>
/// HTTP Basic sobre el canal SAP-facing (spec sección 10). 401 + WWW-Authenticate: Basic si faltan
/// credenciales, son inválidas o el usuario está deshabilitado. Los intentos fallidos se registran solo
/// en el log de aplicación, nunca en BuzonEntrada.
/// </summary>
public sealed class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Basic";

    private readonly IUserGate _userGate;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserGate userGate)
        : base(options, logger, encoder)
    {
        _userGate = userGate;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var headerValue = authorizationHeader.ToString();
        if (!headerValue.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string username;
        string password;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue["Basic ".Length..].Trim()));
            var separatorIndex = decoded.IndexOf(':');
            if (separatorIndex < 0)
            {
                return Task.FromResult(AuthenticateResult.Fail("Encabezado Authorization con formato inválido."));
            }

            username = decoded[..separatorIndex];
            password = decoded[(separatorIndex + 1)..];
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Encabezado Authorization con formato inválido."));
        }

        if (!_userGate.TryAuthenticate(username, password, out var role))
        {
            Logger.LogWarning(
                "Intento de autenticación fallido para el usuario {Username} desde {RemoteIp}.",
                username,
                Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("Usuario o contraseña inválidos."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, role.ToString())],
            Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"KnappMiddleware\"";
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
