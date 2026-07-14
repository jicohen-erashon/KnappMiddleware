using KnappMiddleware.Domain.Auth;
using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserGate _gate;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserGate gate, ILogger<AuthController> logger)
    {
        _gate = gate;
        _logger = logger;
    }

    [HttpPost("reload")]
    public async Task<IActionResult> Reload(CancellationToken cancellationToken)
    {
        try
        {
            await _gate.ReloadAsync(cancellationToken);
            return Ok(new { reloaded = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron recargar los usuarios.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudieron recargar los usuarios.");
        }
    }
}
