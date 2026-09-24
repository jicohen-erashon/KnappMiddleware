using KnappMiddleware.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Controllers.Knapp.Admin.Audit;

[ApiController]
[Route("api/v1/audit")]
[Tags("Audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditReader _reader;
    private readonly ClsAuditToggle _toggle;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditReader reader, ClsAuditToggle toggle, ILogger<AuditController> logger)
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
    public async Task<IActionResult> Toggle([FromBody] AuditToggleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _toggle.SetEnabledAsync(request.Enabled, cancellationToken);
            return Ok(new { enabled = _toggle.IsEnabled });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo actualizar el flag de auditoría.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo actualizar el flag de auditoría.");
        }
    }
}

public sealed record AuditToggleRequest(bool Enabled);
