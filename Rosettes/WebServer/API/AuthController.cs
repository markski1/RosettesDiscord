using Microsoft.AspNetCore.Mvc;
using Rosettes.Database;
using Rosettes.Modules.Engine;

namespace Rosettes.WebServer.API;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpGet("request")]
    public async Task<dynamic> Identify(string applicationKey, ulong userId)
    {
        var appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData == null)
        {
            return GenericResponse.Error("application_not_found");
        }

        var user = await UserEngine.GetDbUserById(userId);

        if (!user.IsValid())
        {
            return GenericResponse.Error("");
        }

        var rel = await AuthRepository.GetApplicationRelation(applicationKey, user.Id);
        
        if (rel == null)
        {
            return user;
        }
        
        bool success = await AuthEngine.RequestApplicationAuth(appData.Name, "Login", user);
        
        if (success) return GenericResponse.Success("request_made");
        else return GenericResponse.Error("request_failed");
    }


    [HttpGet("user")]
    public async Task<dynamic> GetUser(string applicationKey, ulong userId)
    {
        var appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData == null)
        {
            return GenericResponse.Error("application_not_found");
        }

        var user = await UserEngine.GetDbUserById(userId);

        if (!user.IsValid())
        {
            return GenericResponse.Error("user_unknown");
        }

        var rel = await AuthRepository.GetApplicationRelation(applicationKey, user.Id);

        if (rel == null) return GenericResponse.Error("user_not_authorized");

        return user;
    }

    [HttpPost("notify")]
    public async Task<dynamic> NotifyUser(string applicationKey, ulong userId, string message)
    {
        var appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData == null)
        {
            return GenericResponse.Error("application_not_found");
        }
        
        var user = await UserEngine.GetDbUserById(userId);

        if (!user.IsValid())
        {
            return GenericResponse.Error("user_unknown");
        }

        if (message.Length > 2000)
        {
            return GenericResponse.Error("message_too_long");
        }
        
        bool success = await AuthEngine.SendApplicationNotification(appData.Name, message, user);
        
        if (success) return GenericResponse.Success("notification_sent");
        else return GenericResponse.Error("notification_not_sent");
    }
}