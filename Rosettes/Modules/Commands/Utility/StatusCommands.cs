using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Net.NetworkInformation;

namespace Rosettes.Modules.Commands.Utility;

[Group("status", "Status checking commands")]
public class StatusCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("website", "Check if a given website is online and responding to http requests.")]
    public async Task CheckUri(string url)
    {
        if (!url.Contains("http"))
        {
            url = $"https://{url}";
        }
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Head,
            RequestUri = new Uri(url)
        };

        EmbedBuilder embed = await Global.MakeRosettesEmbed();
        embed.Title = "Checking website status";
        embed.Description = $"Checking `{url}`";
        await RespondAsync(embed: embed.Build());

        embed.Title = "Website status";

        // try to get a response off the url
        try
        {
            using var response = await Global.HttpClient.SendAsync(request);
            if ((int)response.StatusCode >= 400)
            {
                embed.Description = $"{url} is offline or unavailable.";
            }
            else
            {
                embed.Description = $"{url} is reachable.";
                embed.AddField("Status code", $"{response.StatusCode} ({(int)response.StatusCode})");
            }
        }
        catch
        {
            embed.Description = $"{url} is unreachable.";
        }

        // report the error by updating the original response. if we don't have access to modify, send it as a follow-up.
        try
        {
            await ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
        }
        catch
        {
            await FollowupAsync("Sending as follow-up, because I don't have message access in this channel.", embed: embed.Build());
        }
    }

    [SlashCommand("ping", "Check if a given hostname or IP address is online.")]
    public async Task CheckPing([Summary("address", "Hostname or IP address to ping.")] string address)
    {
        Ping ping = new();

        EmbedBuilder embed = await Global.MakeRosettesEmbed();

        embed.Title = "Ping";

        embed.Description = $"Pinging {address}";

        try
        {
            var reply = ping.Send(address, 2500);
            
            embed.AddField("Status", reply.Status);
            embed.AddField("Roundtrip latency", $"{reply.RoundtripTime}ms");
        }
        catch
        {
            embed.AddField("Status", "FAIL.\nDestination did not respond to ping.");
        }

        await RespondAsync(embed: embed.Build());
    }
}