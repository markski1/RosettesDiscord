using Discord.WebSocket;
using Discord;
using Microsoft.AspNetCore.Mvc;
using Rosettes.Core;
using Rosettes.Managers;
using Rosettes.Modules.Engine.Guild;
using Rosettes.Modules.Engine;

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

        foreach (var guild in GuildEngine.GuildCache) {
            ret += $"{guild.NameCache} | {guild.Members} members | {(await UserEngine.GetUserReferenceByID(guild.OwnerId)).Username}\n\n";
            userSum += guild.Members;
        }

        ret += $"Servers: {GuildEngine.GuildCache.Count} | Total users served: {userSum}";

        return ret;
    }

    [HttpGet("SendMessage")]
    public string SendMessage(string secretKey, ulong channelId, string message)
    {
        if (secretKey != Settings.SecretKey)
        {
            return "No secret key provided.";
        }

        try
        {
            // send it to error channel
            var client = ServiceManager.GetService<DiscordSocketClient>();
            if (client.GetChannel(channelId) is not ITextChannel destinationChannel) return "Channel could not be found.";
            destinationChannel.SendMessageAsync(message);
            return "Message was sent, probably.";
        }
        catch (Exception ex)
        {
            return $"Message could not be sent. \n {ex}";
        }        
    }

    [HttpGet("CheckKey")]
    public async Task<User> CheckKey(string rosettesKey)
    {
        rosettesKey = rosettesKey.Trim();
        return await UserEngine.GetUserByRosettesKey(rosettesKey);
    }
}