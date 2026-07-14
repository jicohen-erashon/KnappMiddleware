using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Api.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{    
    [HttpGet("health")]
    public IActionResult Get() => Ok(new { status = "Healthy" });
}
