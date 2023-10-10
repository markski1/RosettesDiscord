using Microsoft.AspNetCore.Mvc;

namespace Rosettes.Controllers;

[ApiController]
[Route("rosettes-api")]
public class ApiController : ControllerBase
{
    private readonly ILogger<ApiController> _logger;

    public ApiController(ILogger<ApiController> logger)
    {
        _logger = logger;
    }

    [HttpGet("CheckAlive")]
    public string CheckAlive()
    {
        return "Rosettes lives!";
    }
}