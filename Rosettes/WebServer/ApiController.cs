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

        var client = ServiceManager.GetService<DiscordSocketClient>();

        foreach (var guild in GuildEngine.GuildCache) {
            if (client.Guilds.Where(x => x.Id == guild.Id).Any())
            {
                ret += $"{guild.NameCache} | {guild.Members} members | {(await UserEngine.GetUserReferenceByID(guild.OwnerId)).Username}\n\n";
                userSum += guild.Members;
                serverSum += 1;
            }
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
                if (client.GetUser(channelId) is not SocketUser user) return "User could not be found";
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
        var repo = new AuthRepository();

        var appData = await repo.GetApplicationAuth(applicationKey);

        if (appData == null)
        {
            return GenericResponse.GenerateError("Application key does not exist.");
        }

        var user = await UserEngine.GetUserByRosettesKey(userKey);

        if (!user.IsValid())
        {
            return GenericResponse.GenerateError("User key does not exist.");
        }

        var rel = repo.GetApplicationRelation(applicationKey, user.Id);

        if (rel == null)
        {
            await repo.AuthUser(appData.Id, user.Id);

            await user.SendDirectMessage($"Auth notice: ```Your Rosettes key has been used to authorize you to the following application: {appData.Name}```");
        }

        return user;
    }


    [HttpGet("GetUser")]
    public async Task<dynamic> Identify(string applicationKey, ulong userId)
    {
        var repo = new AuthRepository();
        var appData = await repo.GetApplicationAuth(applicationKey);

        if (appData == null)
        {
            return GenericResponse.GenerateError("Application key does not exist.");
        }

        var user = UserEngine.GetDBUserById(userId);

        if (!user.IsValid())
        {
            return GenericResponse.GenerateError("Rosettes doesn't know that user ID.");
        }

        var rel = repo.GetApplicationRelation(applicationKey, user.Id);

        if (rel == null)
        {
            return GenericResponse.GenerateError("The user ID provided has not authorized your application.");
        }

        return user;
    }
}

public static class GenericResponse
{
    public static Dictionary<string, dynamic> GenerateError(string message)
    {
        return new Dictionary<string, dynamic>()
        {
            {"success", false},
            {"message", message}
        };
    }
}