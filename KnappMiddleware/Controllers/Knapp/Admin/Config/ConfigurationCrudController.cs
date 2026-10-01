using System.Text.Json;
using KnappMiddleware.Admin;
using KnappMiddleware.Auditing;
using KnappMiddleware.Configuration;
using KnappMiddleware.Contratos.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Controllers.Knapp.Admin.Config;

/// <summary>
/// CRUD sobre la tabla configuracion (fuente de verdad que el <see cref="ClsConfigGate"/>
/// recarga en caliente). Complementa a <c>ConfigController</c>: este expone filas
/// individuales (CRUD); el primero expone vistas agregadas por subsistema.
///
/// Endpoints (todos requieren rol SuperUsuario vía FallbackPolicy):
///   GET    /api/v1/configurations               — listar todas
///   GET    /api/v1/configurations/{clave}       — obtener una
///   POST   /api/v1/configurations               — crear (falla si ya existe)
///   PUT    /api/v1/configurations/{clave}       — actualizar (upsert)
///   DELETE /api/v1/configurations/{clave}       — eliminar (rechazado si es clave crítica)
///
/// Cada acción (excepto GET) registra en <see cref="ClsAuditWriter"/> sin bloquear la respuesta.
/// </summary>
[ApiController]
[Route("api/v1/configurations")]
[Tags("Configurations")]
public sealed class ConfigurationCrudController : ControllerBase
{
    private readonly IConfigRepository _repository;
    private readonly ClsConfigGate _gate;
    private readonly ClsAuditWriter _auditWriter;
    private readonly IAuditReader _auditReader;
    private readonly ILogger<ConfigurationCrudController> _logger;

    public ConfigurationCrudController(
        IConfigRepository repository,
        ClsConfigGate gate,
        ClsAuditWriter auditWriter,
        IAuditReader auditReader,
        ILogger<ConfigurationCrudController> logger)
    {
        _repository = repository;
        _gate = gate;
        _auditWriter = auditWriter;
        _auditReader = auditReader;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfiguracionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        try
        {
            var rows = await _repository.GetAllAsync(ct);
            var dtos = rows
                .Select(r => new ConfiguracionDto(r.Clave, r.Valor, r.Descripcion, r.CreadoEn, r.ActualizadoEn))
                .ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo listar la tabla configuracion.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo listar la tabla configuracion.");
        }
    }

    [HttpGet("{clave}")]
    [ProducesResponseType(typeof(ConfiguracionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(string clave, CancellationToken ct)
    {
        var v = AdminGuardrails.ValidateClave(clave);
        if (!v.IsValid) return BadRequest(new { error = v.Error });

        try
        {
            var rows = await _repository.GetAllAsync(ct);
            var row = rows.FirstOrDefault(r => r.Clave.Equals(clave, StringComparison.OrdinalIgnoreCase));
            if (row is null) return NotFound(new { clave, error = "No existe la clave solicitada." });
            return Ok(new ConfiguracionDto(row.Clave, row.Valor, row.Descripcion, row.CreadoEn, row.ActualizadoEn));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo obtener {Clave}.", clave);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo obtener la clave.");
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConfiguracionCambiadaResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] CrearConfiguracionRequest request, CancellationToken ct)
    {
        var vc = AdminGuardrails.ValidateClave(request.Clave);
        if (!vc.IsValid) return BadRequest(new { error = vc.Error });
        var vv = AdminGuardrails.ValidateValor(request.Clave, request.Valor ?? string.Empty);
        if (!vv.IsValid) return BadRequest(new { error = vv.Error });

        try
        {
            var rows = await _repository.GetAllAsync(ct);
            if (rows.Any(r => r.Clave.Equals(request.Clave, StringComparison.OrdinalIgnoreCase)))
                return Conflict(new { clave = request.Clave, error = "La clave ya existe. Use PUT para actualizar." });

            await _repository.SetValueAsync(request.Clave, request.Valor ?? string.Empty, ct);
            await _gate.ReloadAsync(ct);

            var now = DateTime.UtcNow;
            var dto = new ConfiguracionDto(request.Clave, request.Valor ?? string.Empty, request.Descripcion, now, now);
            AdminAuditHook.Record(_auditWriter, User, "create", request.Clave, null, request.Valor);
            _logger.LogInformation("Config {Clave} creada por {User}.", request.Clave, User.Identity?.Name ?? "anonymous");
            return CreatedAtAction(nameof(Obtener), new { clave = request.Clave }, new ConfiguracionCambiadaResult(dto, Created: true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo crear {Clave}.", request.Clave);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo crear la clave.");
        }
    }

    [HttpPut("{clave}")]
    [ProducesResponseType(typeof(ConfiguracionCambiadaResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Actualizar(string clave, [FromBody] ActualizarConfiguracionRequest request, CancellationToken ct)
    {
        var vc = AdminGuardrails.ValidateClave(clave);
        if (!vc.IsValid) return BadRequest(new { error = vc.Error });
        var vv = AdminGuardrails.ValidateValor(clave, request.Valor ?? string.Empty);
        if (!vv.IsValid) return BadRequest(new { error = vv.Error });

        try
        {
            var rows = await _repository.GetAllAsync(ct);
            var existing = rows.FirstOrDefault(r => r.Clave.Equals(clave, StringComparison.OrdinalIgnoreCase));
            var isCreate = existing is null;
            if (isCreate)
                _logger.LogInformation("PUT sobre clave inexistente {Clave} — se crea.", clave);

            await _repository.SetValueAsync(clave, request.Valor ?? string.Empty, ct);
            await _gate.ReloadAsync(ct);

            var now = DateTime.UtcNow;
            var dto = new ConfiguracionDto(clave, request.Valor ?? string.Empty, request.Descripcion, existing?.CreadoEn ?? now, now);
            AdminAuditHook.Record(_auditWriter, User, isCreate ? "create" : "update", clave, existing?.Valor, request.Valor);
            _logger.LogInformation("Config {Clave} {Op} por {User}.", clave, isCreate ? "creada" : "actualizada", User.Identity?.Name ?? "anonymous");
            return Ok(new ConfiguracionCambiadaResult(dto, Created: isCreate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo actualizar {Clave}.", clave);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo actualizar la clave.");
        }
    }

    [HttpDelete("{clave}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(string clave, CancellationToken ct)
    {
        var vc = AdminGuardrails.ValidateClave(clave);
        if (!vc.IsValid) return BadRequest(new { error = vc.Error });

        if (!AdminGuardrails.CanDelete(clave))
            return BadRequest(new { clave, error = "Clave crítica: no se puede eliminar. Use PUT para modificar." });

        try
        {
            var rows = await _repository.GetAllAsync(ct);
            var existing = rows.FirstOrDefault(r => r.Clave.Equals(clave, StringComparison.OrdinalIgnoreCase));
            if (existing is null) return NotFound(new { clave, error = "No existe la clave solicitada." });

            await _repository.DeleteValueAsync(clave, ct);
            await _gate.ReloadAsync(ct);

            AdminAuditHook.Record(_auditWriter, User, "delete", clave, existing.Valor, null);
            _logger.LogWarning("Config {Clave} ELIMINADA por {User}.", clave, User.Identity?.Name ?? "anonymous");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo eliminar {Clave}.", clave);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo eliminar la clave.");
        }
    }

    /// <summary>
    /// Historial de cambios para una clave específica. Filtra de BuzonEntrada los registros cuyo
    /// TipoTelegrama empieza por "ADMIN/" y cuyo payload incluye la clave afectada. Devuelve
    /// los N más recientes (top-down) — sirve al drawer de historial del panel admin.
    /// </summary>
    [HttpGet("{clave}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditQueryResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> History(string clave, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var vc = AdminGuardrails.ValidateClave(clave);
        if (!vc.IsValid) return BadRequest(new { error = vc.Error });

        try
        {
            var needle = $"\"clave\":\"{clave}\"";
            var rows = await _auditReader.QueryAsync(
                new AuditQuery(AuditDireccion.Entrada, CorrelationId: null, Take: 500), ct);
            var filtered = rows
                .Where(r => r.TipoTelegrama.StartsWith("ADMIN/", StringComparison.OrdinalIgnoreCase)
                         && r.Payload is not null
                         && r.Payload.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreadoEn)
                .Take(Math.Clamp(take, 1, 200))
                .ToList();
            return Ok(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo obtener el historial de {Clave}.", clave);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo obtener el historial.");
        }
    }
}
