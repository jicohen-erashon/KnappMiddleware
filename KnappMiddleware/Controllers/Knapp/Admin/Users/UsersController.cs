using KnappMiddleware.Admin;
using KnappMiddleware.Auditing;
using KnappMiddleware.Auth;
using KnappMiddleware.Contratos.Admin;
using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Controllers.Knapp.Admin.Users;

/// <summary>
/// CRUD sobre la tabla usuarios desde el panel admin (alta, cambio de rol/habilitado, reset de
/// contraseña, baja). Complementa a <see cref="ClsUserGate"/>, que sigue siendo el snapshot en memoria
/// que el hot-path de autenticación (Basic + Cookie) consulta — cada mutación acá recarga ese gate.
///
/// Endpoints (todos requieren rol SuperUsuario vía FallbackPolicy):
///   GET    /api/v1/users                          — listar (sin hash de contraseña)
///   POST   /api/v1/users                          — crear (falla si ya existe)
///   PUT    /api/v1/users/{nombreUsuario}          — actualizar rol + habilitado
///   POST   /api/v1/users/{nombreUsuario}/reset-password — restablecer contraseña
///   DELETE /api/v1/users/{nombreUsuario}          — eliminar
///
/// Cambiar rol/habilitado/eliminar corre <see cref="UserGuardrails.CanChangeOrDelete"/> para evitar
/// dejar el panel sin ningún SuperUsuario habilitado (auto-lockout).
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Tags("Users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRepository _repository;
    private readonly ClsUserGate _gate;
    private readonly ClsAuditWriter _auditWriter;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRepository repository,
        ClsUserGate gate,
        ClsAuditWriter auditWriter,
        ILogger<UsersController> logger)
    {
        _repository = repository;
        _gate = gate;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        try
        {
            var rows = await _repository.GetAllForAdminAsync(ct);
            var dtos = rows
                .Select(r => new UsuarioDto(r.NombreUsuario, r.Rol.ToString(), r.Habilitado, r.CreadoEn, r.ActualizadoEn))
                .ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo listar la tabla usuarios.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo listar la tabla usuarios.");
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioRequest request, CancellationToken ct)
    {
        var vn = UserGuardrails.ValidateNombreUsuario(request.NombreUsuario);
        if (!vn.IsValid) return BadRequest(new { error = vn.Error });
        var vc = UserGuardrails.ValidateContrasena(request.Contrasena);
        if (!vc.IsValid) return BadRequest(new { error = vc.Error });
        var vr = UserGuardrails.ValidateRol(request.Rol);
        if (!vr.IsValid) return BadRequest(new { error = vr.Error });

        try
        {
            var actuales = await _repository.GetAllForAdminAsync(ct);
            if (actuales.Any(u => u.NombreUsuario.Equals(request.NombreUsuario, StringComparison.OrdinalIgnoreCase)))
                return Conflict(new { nombreUsuario = request.NombreUsuario, error = "El usuario ya existe." });

            await _repository.CreateAsync(request.NombreUsuario, request.Contrasena, request.Rol, ct);
            await _gate.ReloadAsync(ct);

            var now = DateTime.UtcNow;
            var dto = new UsuarioDto(request.NombreUsuario, request.Rol, Habilitado: true, now, now);
            AdminAuditHook.RecordUsuario(_auditWriter, User, "create", request.NombreUsuario, null, request.Rol);
            _logger.LogInformation("Usuario {NombreUsuario} creado por {User}.", request.NombreUsuario, User.Identity?.Name ?? "anonymous");
            return CreatedAtAction(nameof(Listar), null, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo crear el usuario {NombreUsuario}.", request.NombreUsuario);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo crear el usuario.");
        }
    }

    [HttpPut("{nombreUsuario}")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(string nombreUsuario, [FromBody] ActualizarUsuarioRequest request, CancellationToken ct)
    {
        var vr = UserGuardrails.ValidateRol(request.Rol);
        if (!vr.IsValid) return BadRequest(new { error = vr.Error });

        try
        {
            var actuales = await _repository.GetAllForAdminAsync(ct);
            var existing = actuales.FirstOrDefault(u => u.NombreUsuario.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase));
            if (existing is null) return NotFound(new { nombreUsuario, error = "No existe el usuario solicitado." });

            var permanece = request.Rol == nameof(UserRole.SuperUsuario) && request.Habilitado;
            var lockCheck = UserGuardrails.CanChangeOrDelete(actuales, nombreUsuario, permanece);
            if (!lockCheck.IsValid) return BadRequest(new { error = lockCheck.Error });

            await _repository.UpdateAsync(nombreUsuario, request.Rol, request.Habilitado, ct);
            await _gate.ReloadAsync(ct);

            var dto = new UsuarioDto(nombreUsuario, request.Rol, request.Habilitado, existing.CreadoEn, DateTime.UtcNow);
            AdminAuditHook.RecordUsuario(_auditWriter, User, "update", nombreUsuario, existing.Rol.ToString(), request.Rol);
            _logger.LogInformation("Usuario {NombreUsuario} actualizado por {User}.", nombreUsuario, User.Identity?.Name ?? "anonymous");
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo actualizar el usuario {NombreUsuario}.", nombreUsuario);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo actualizar el usuario.");
        }
    }

    [HttpPost("{nombreUsuario}/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(string nombreUsuario, [FromBody] RestablecerContrasenaUsuarioRequest request, CancellationToken ct)
    {
        var vc = UserGuardrails.ValidateContrasena(request.ContrasenaNueva);
        if (!vc.IsValid) return BadRequest(new { error = vc.Error });

        try
        {
            var actuales = await _repository.GetAllForAdminAsync(ct);
            var existing = actuales.FirstOrDefault(u => u.NombreUsuario.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase));
            if (existing is null) return NotFound(new { nombreUsuario, error = "No existe el usuario solicitado." });

            await _repository.ResetPasswordAsync(nombreUsuario, request.ContrasenaNueva, ct);
            // El hash viejo sigue vivo en el snapshot del Gate hasta este reload — sin esto, la
            // contraseña anterior seguiría autenticando hasta el próximo recargo natural.
            await _gate.ReloadAsync(ct);

            AdminAuditHook.RecordUsuario(_auditWriter, User, "reset_password", nombreUsuario, null, null);
            _logger.LogWarning("Contraseña de {NombreUsuario} restablecida por {User}.", nombreUsuario, User.Identity?.Name ?? "anonymous");
            return Ok(new { nombreUsuario, reset = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo restablecer la contraseña de {NombreUsuario}.", nombreUsuario);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo restablecer la contraseña.");
        }
    }

    [HttpDelete("{nombreUsuario}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(string nombreUsuario, CancellationToken ct)
    {
        try
        {
            var actuales = await _repository.GetAllForAdminAsync(ct);
            var existing = actuales.FirstOrDefault(u => u.NombreUsuario.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase));
            if (existing is null) return NotFound(new { nombreUsuario, error = "No existe el usuario solicitado." });

            var lockCheck = UserGuardrails.CanChangeOrDelete(actuales, nombreUsuario, permaneceSuperUsuarioHabilitado: false);
            if (!lockCheck.IsValid) return BadRequest(new { error = lockCheck.Error });

            var deleted = await _repository.DeleteAsync(nombreUsuario, ct);
            if (!deleted) return NotFound(new { nombreUsuario, error = "No existe el usuario solicitado." });

            await _gate.ReloadAsync(ct);

            AdminAuditHook.RecordUsuario(_auditWriter, User, "delete", nombreUsuario, existing.Rol.ToString(), null);
            _logger.LogWarning("Usuario {NombreUsuario} ELIMINADO por {User}.", nombreUsuario, User.Identity?.Name ?? "anonymous");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo eliminar el usuario {NombreUsuario}.", nombreUsuario);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo eliminar el usuario.");
        }
    }
}
