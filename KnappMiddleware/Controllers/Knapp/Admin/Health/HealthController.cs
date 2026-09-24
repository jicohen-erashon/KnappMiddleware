using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KnappMiddleware.Controllers.Knapp.Admin.Health;

[ApiController]
[AllowAnonymous]
[Tags("Health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Get() => Ok(new { status = "Healthy" });
}
