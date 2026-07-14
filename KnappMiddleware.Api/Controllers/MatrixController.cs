using KnappMiddleware.Domain.Matrix;
using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Api.Controllers;

[ApiController]
[Route("matrix")]
public sealed class MatrixController : ControllerBase
{
    private readonly IMatrixRepository _repository;
    private readonly IMatrixGate _gate;
    private readonly ILogger<MatrixController> _logger;

    public MatrixController(IMatrixRepository repository, IMatrixGate gate, ILogger<MatrixController> logger)
    {
        _repository = repository;
        _gate = gate;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _repository.GetAllAsync(cancellationToken);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo consultar la matriz.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo consultar la matriz.");
        }
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
            _logger.LogError(ex, "No se pudo recargar la matriz.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo recargar la matriz.");
        }
    }
}
