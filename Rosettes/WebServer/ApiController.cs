using Discord.WebSocket;
using Discord;
using Microsoft.AspNetCore.Mvc;
using Rosettes.Core;
using Rosettes.Managers;
using Rosettes.Modules.Engine.Guild;
using Rosettes.Modules.Engine;
using Rosettes.Database;

namespace Rosettes.WebServer;

[ApiController]
[Route("rosettes-api")]
public class ApiController : ControllerBase
{
    [HttpGet("CheckAlive")]
    public string CheckAlive()
    {
        return "Rosettes lives!";
    }

    [HttpGet("ServerFetch")]
    public async Task<string> ServerFetch(string secretKey = "?")
    {
        if (secretKey != Settings.SecretKey)
        {
            return "No secret key provided.";
        }

        string ret = "";

        ulong userSum = 0;
        int serverSum = 0;

        foreach (var guild in GuildEngine.GetActiveGuilds())
        {
            var findOwner = await UserEngine.GetUserReferenceById(guild.OwnerId);
            if (findOwner != null)
            {
                ret += $"{guild.NameCache} | {guild.Members} members | {findOwner.Username}\n\n";
            }
            userSum += guild.Members;
            serverSum += 1;
        }

        ret += $"Servers: {serverSum} | Total users served: {userSum}";

        return ret;
    }

    [HttpGet("SendMessage")]
    public string SendMessage(string secretKey, ulong channelId, string message, int type = 0)
    {
        if (secretKey != Settings.SecretKey)
        {
            return "No secret key provided.";
        }

        try
        {
            var client = ServiceManager.GetService<DiscordSocketClient>();
            if (type == 0)
            {
                if (client.GetChannel(channelId) is not ITextChannel destinationChannel) return "Channel could not be found.";
                destinationChannel.SendMessageAsync(message);
            }
            else
            {
                if (client.GetUser(channelId) is not { } user) return "User could not be found";
                user.SendMessageAsync(message);
            }
            return "Message was sent, probably.";
        }
        catch (Exception ex)
        {
            return $"Message could not be sent. \n {ex}";
        }        
    }

    [HttpGet("Identify")]
    public async Task<dynamic> Identify(string applicationKey, string userKey)
    {
        userKey = userKey.Trim();

        var appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData == null)
        {
            return GenericResponse.GenerateError("Application key does not exist.");
        }

        var user = await UserEngine.GetUserByRosettesKey(userKey);

        if (!user.IsValid())
        {
            return GenericResponse.GenerateError("User key does not exist.");
        }

        var rel = await AuthRepository.GetApplicationRelation(applicationKey, user.Id);

        if (rel != null) return user;
        
        await AuthRepository.AuthUser(appData.Id, user.Id);

        await user.SendDirectMessage($"Auth notice: ```Your Rosettes key has been used to authorize you to the following application: {appData.Name}```");

        return user;
    }


    [HttpGet("GetUser")]
    public async Task<dynamic> Identify(string applicationKey, ulong userId)
    {
        var appData = await AuthRepository.GetApplicationAuth(applicationKey);

        if (appData == null)
        {
            return GenericResponse.GenerateError("Application key does not exist.");
        }

        var user = UserEngine.GetDbUserById(userId);

        if (!user.IsValid())
        {
            return GenericResponse.GenerateError("Rosettes doesn't know that user ID.");
        }

        var rel = await AuthRepository.GetApplicationRelation(applicationKey, user.Id);

        if (rel != null) return user;
        
        return GenericResponse.GenerateError("The user ID provided has not authorized your application.");

    }
}

public static class GenericResponse
{
    public static Dictionary<string, dynamic> GenerateError(string message)
    {
        return new Dictionary<string, dynamic>
        {
            {"success", false},
            {"message", message}
        };
    }
}