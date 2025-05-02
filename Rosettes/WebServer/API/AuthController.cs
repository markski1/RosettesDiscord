using Microsoft.AspNetCore.Mvc;
using Rosettes.Database;
using Rosettes.Modules.Engine;

namespace Rosettes.WebServer.API;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpGet("request")]
    public async Task<dynamic> RequestAuth(string applicationKey, ulong userId)
    {
        ApplicationAuth? appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData is null)
        {
            return GenericResponse.Error("application_not_found");
        }

        var user = await UserEngine.GetDbUserById(userId);

        if (!user.IsValid())
        {
            return GenericResponse.Error("user_not_found");
        }

        // Check if this application-user relation already exists.
        ApplicationRelation? rel = await AuthRepository.GetApplicationRelation(applicationKey, user.Id);
        
        if (rel is not null)
        {
            return GenericResponse.Success("user_already_authorized");
        }
        
        bool success = await AuthEngine.RequestApplicationAuth(appData, user);
        
        if (success) return GenericResponse.Success("request_made");
        else return GenericResponse.Error("request_failed");
    }


    [HttpGet("user")]
    public async Task<dynamic> GetUser(string applicationKey, ulong userId)
    {
        ApplicationAuth? appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData is null)
        {
            return GenericResponse.Error("application_not_found");
        }

        var user = await UserEngine.GetDbUserById(userId);

        if (!user.IsValid())
        {
            return GenericResponse.Error("user_unknown");
        }

        ApplicationRelation? rel = await AuthRepository.GetApplicationRelation(applicationKey, user.Id);

        if (rel is null) return GenericResponse.Error("user_not_authorized");

        return user;
    }

    [HttpPost("notify")]
    public async Task<dynamic> NotifyUser(string applicationKey, ulong userId, string message)
    {
        ApplicationAuth? appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData is null)
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