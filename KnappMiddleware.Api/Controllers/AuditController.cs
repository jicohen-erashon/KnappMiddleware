using KnappMiddleware.Domain.Auditing;
using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Api.Controllers;

[ApiController]
[Route("audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditReader _reader;
    private readonly IAuditToggle _toggle;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditReader reader, IAuditToggle toggle, ILogger<AuditController> logger)
    {
        _reader = reader;
        _toggle = toggle;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string direccion,
        [FromQuery] Guid? correlationId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AuditDireccion>(direccion, ignoreCase: true, out var direccionValue))
        {
            return BadRequest(new { error = "El parámetro 'direccion' debe ser 'Entrada' o 'Salida'." });
        }

        try
        {
            var results = await _reader.QueryAsync(new AuditQuery(direccionValue, correlationId, take), cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo consultar la auditoría.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo consultar la auditoría.");
        }
    }

    [HttpPut("toggle")]
    public IActionResult Toggle([FromBody] AuditToggleRequest request)
    {
        _toggle.SetEnabled(request.Enabled);
        return Ok(new { enabled = _toggle.IsEnabled });
    }
}

public sealed record AuditToggleRequest(bool Enabled);
