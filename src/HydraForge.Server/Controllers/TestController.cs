using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers;

[ApiController]
public class TestController : ControllerBase
{
    [HttpGet("/weatherforecast")]
    public IActionResult GetWeatherForecast()
    {
        return Ok(new { temperature = 20 });
    }

    [HttpGet("/throw")]
    public IActionResult Throw()
    {
        throw new InvalidOperationException("Test exception");
    }
}