using KnappMiddleware.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Controllers.Knapp.Admin.Auth;

[ApiController]
[Route("api/v1/auth")]
[Tags("Auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ClsUserGate _gate;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ClsUserGate gate, ILogger<AuthController> logger)
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
