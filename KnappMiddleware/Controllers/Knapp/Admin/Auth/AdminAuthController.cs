using System.Security.Claims;
using KnappMiddleware.Auth;
using KnappMiddleware.Contratos.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Controllers.Knapp.Admin.Auth;

/// <summary>
/// Login/logout del panel administrativo (wwwroot/admin). Sustituye el prompt nativo de Basic Auth
/// por una pantalla de login explícita (wwwroot/admin/login.html) respaldada por una cookie de sesión.
/// Solo el rol SuperUsuario puede entrar al panel; el canal SAP-facing sigue usando Basic Auth
/// (ver <see cref="BasicAuthenticationHandler"/>) y no pasa por aquí.
/// </summary>
[ApiController]
[Route("api/v1/admin/auth")]
[Tags("AdminAuth")]
public sealed class AdminAuthController : ControllerBase
{
    private readonly ClsUserGate _userGate;
    private readonly ILogger<AdminAuthController> _logger;

    public AdminAuthController(ClsUserGate userGate, ILogger<AdminAuthController> logger)
    {
        _userGate = userGate;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AdminSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { error = "Usuario y contraseña son requeridos." });

        if (!_userGate.TryAuthenticate(request.Username, request.Password, out var role) || role != UserRole.SuperUsuario)
        {
            _logger.LogWarning(
                "Intento de login fallido al panel admin para {Username} desde {RemoteIp}.",
                request.Username,
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "Usuario o contraseña inválidos." });
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, request.Username), new Claim(ClaimTypes.Role, role.ToString())],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        _logger.LogInformation("Login exitoso al panel admin: {Username}.", request.Username);
        return Ok(new AdminSessionDto(request.Username, role.ToString()));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(AdminSessionDto), StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        var username = User.Identity?.Name ?? string.Empty;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        return Ok(new AdminSessionDto(username, role));
    }
}
