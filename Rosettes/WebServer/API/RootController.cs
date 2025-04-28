using Discord.WebSocket;
using Discord;
using Microsoft.AspNetCore.Mvc;
using Rosettes.Core;
using Rosettes.Managers;

namespace Rosettes.WebServer;

[ApiController]
[Route("api")]
public class RootController : ControllerBase
{
    [HttpGet("alive")]
    public string CheckAlive()
    {
        return "Rosettes lives!";
    }

    [HttpGet("message")]
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
}