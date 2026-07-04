using Microsoft.AspNetCore.Mvc;
using Rosettes.Database;
using Rosettes.Modules.Engine;

namespace Rosettes.WebServer.API;

[ApiController]
[Route("rosapi/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("request")]
    public async Task<IActionResult> RequestAuth(string applicationKey, ulong userId)
    {
        ApplicationAuth? appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData is null)
        {
            return Unauthorized(ApiResponse.Error("application_not_found"));
        }

        var user = await UserEngine.GetDbUserById(userId);

        if (!user.IsValid())
        {
            return NotFound(ApiResponse.Error("user_not_found"));
        }

        ApplicationRelation? rel = await AuthRepository.GetApplicationRelation(applicationKey, user.Id);
        if (rel is not null)
        {
            return Ok(ApiResponse.Success("user_already_authorized"));
        }

        bool success = await AuthEngine.RequestApplicationAuth(appData, user);
        if (!success)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ApiResponse.Error("request_failed"));
        }

        return Ok(ApiResponse.Success("request_made"));
    }

    [HttpGet("user")]
    public async Task<IActionResult> GetUser(string applicationKey, ulong userId)
    {
        ApplicationAuth? appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData is null)
        {
            return Unauthorized(ApiResponse.Error("application_not_found"));
        }

        var user = await UserEngine.GetDbUserById(userId);

        if (!user.IsValid())
        {
            return NotFound(ApiResponse.Error("user_unknown"));
        }

        ApplicationRelation? rel = await AuthRepository.GetApplicationRelation(applicationKey, user.Id);
        if (rel is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Error("user_not_authorized"));
        }

        return Ok(ApiResponse.Success("user", user));
    }

    [HttpPost("notify")]
    public async Task<IActionResult> NotifyUser(string applicationKey, ulong userId, string message)
    {
        ApplicationAuth? appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData is null)
        {
            return Unauthorized(ApiResponse.Error("application_not_found"));
        }

        var user = await UserEngine.GetDbUserById(userId);

        if (!user.IsValid())
        {
            return NotFound(ApiResponse.Error("user_unknown"));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(ApiResponse.Error("message_required"));
        }

        if (message.Length > 2000)
        {
            return BadRequest(ApiResponse.Error("message_too_long"));
        }

        ApplicationRelation? rel = await AuthRepository.GetApplicationRelation(applicationKey, user.Id);
        if (rel is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Error("user_not_authorized"));
        }

        bool success = await AuthEngine.SendApplicationNotification(appData.Name, message, user);
        if (!success)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ApiResponse.Error("notification_not_sent"));
        }

        return Ok(ApiResponse.Success("notification_sent"));
    }
}
