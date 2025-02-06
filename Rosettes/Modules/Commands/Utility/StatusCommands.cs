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
    [SlashCommand("csgo", "Check the status of Steam and CSGO matchmaking.")]
    public async Task CsgoStatus()
    {
        try
        {
            var data = await Global.HttpClient.GetStringAsync($"https://api.steampowered.com/ICSGOServers_730/GetGameServersStatus/v1/?key={Settings.SteamDevKey}");

            var deserialziedObject = JsonConvert.DeserializeObject(data);
            if (deserialziedObject == null)
            {
                await RespondAsync("Failed to retrieve status data.", ephemeral: true);
                return;
            }
            dynamic result = ((dynamic)deserialziedObject).result;

            TimeSpan waitTime = TimeSpan.FromSeconds(Convert.ToDouble(result.matchmaking.search_seconds_avg));

            EmbedBuilder steamStatus = await Global.MakeRosettesEmbed();

            steamStatus.Title = "Steam Status";

            steamStatus.AddField("Logon service", result.services.SessionsLogon, true);

            steamStatus.AddField("Steam Community", result.services.SteamCommunity, true);

            EmbedBuilder csgoStatus = await Global.MakeRosettesEmbed();

            csgoStatus.Title = "CS:GO Status";

            csgoStatus.AddField("Matchmaking", result.matchmaking.scheduler, true);
            csgoStatus.AddField("Online players", $"{result.matchmaking.online_players:N0}", true);
            csgoStatus.AddField("Online servers", $"{result.matchmaking.online_servers:N0}", true);
            csgoStatus.AddField("Searching game", $"{result.matchmaking.searching_players:N0}", true);
            csgoStatus.AddField("Average wait", $"{waitTime.Minutes} minute{(waitTime.Minutes != 1 ? 's' : null)}, {waitTime.Seconds} seconds.\n", true);

            Embed[] embeds = [steamStatus.Build(), csgoStatus.Build()];

            await RespondAsync(embeds: embeds);
        }
        catch
        {
            await RespondAsync("Failed to fetch status data. This might mean steam is down.");
        }
    }

    [SlashCommand("ffxiv", "Check the status of FFXIV servers.")]
    public async Task XivCheck([Summary("datacenter", "Optionally, specify a datacenter to check it's servers.")] string checkServer = "NOTSPECIFIED")
    {
        string lobbyData;
        try
        {
            lobbyData = await Global.HttpClient.GetStringAsync("http://frontier.ffxiv.com/worldStatus/gate_status.json");
        }
        catch
        {
            await RespondAsync("Failed to retrieve datacenter list.", ephemeral: true);
            return;
        }
        var deserializedLobbyObject = JsonConvert.DeserializeObject(lobbyData);
        if (deserializedLobbyObject == null)
        {
            return;
        }

        dynamic lobby = deserializedLobbyObject;

        string lobbyText = $"Lobby status    : {(lobby.status == 1 ? "Online" : "Offline")}";
        string serverText = "";
        if (checkServer == "NOTSPECIFIED")
        {
            serverText = $"For world status, please specify a datacenter name. (/ffxiv <name>)\n";
        }
        else
        {
            string datacenterData;
            string worldData;
            try
            {
                datacenterData = await Global.HttpClient.GetStringAsync($"https://xivapi.com/servers/dc?private_key={Settings.FFXIVApiKey}");
                worldData = await Global.HttpClient.GetStringAsync("http://frontier.ffxiv.com/worldStatus/current_status.json");
            }
            catch
            {
                await RespondAsync("Failed to retrieve datacenter data.", ephemeral: true);
                return;
            }

            JObject datacenterObj;
            JObject worldObj;

            try
            {
                datacenterObj = JObject.Parse(datacenterData);
                worldObj = JObject.Parse(worldData);
            }
            catch
            {
                await RespondAsync("Failed to retrieve datacenter data.", ephemeral: true);
                return;
            }

            string searchTerm = checkServer.ToLower();
            List<string> serverNames = [];

            // This gets ugly really fast. Because of how frontier's xiv api formats the status response, we have to iterate all this crap
            // to figure out what servers we want to look at depending on the world given.
            // In other words, here we grab the datacenter object, with all it's servers are children token.
            foreach (var datacenter in datacenterObj.Cast<KeyValuePair<string, JToken>>().ToList())
            {
                if (!datacenter.Key.Equals(searchTerm, StringComparison.CurrentCultureIgnoreCase)) continue;
                foreach (var server in datacenter.Value)
                {
                    serverNames.Add(server.ToString());
                }
            }
            if (!serverNames.Any())
            {
                serverText = "The specified datacenter was not found.\n";
            }
            else
            {
                // now that we have the name of the servers we care about, we go through the entire list taken off xiv's api
                // we compare text names and put them in when we have a hit.
                foreach (var world in worldObj.Cast<KeyValuePair<string, JToken>>().ToList())
                {
                    if (serverNames.Contains(world.Key))
                    {
                        int spacing = 16 - world.Key.Length;
                        string serverName = $"{world.Key} {new(' ', spacing)}";

                        serverText += $"{serverName}: {((int)world.Value == 1 ? "Online" : "Offline")}\n";
                    }
                }
            }
        }
        string text =
            $"```\n" +
            $"FFXIV Status:\n" +
            $"================\n" +
            $"{lobbyText}\n" +
            $"================\n" +
            $"{serverText}" +
            $"================\n" +
            $"```";
        await RespondAsync(text);
    }

    [SlashCommand("minecraft", "Check the status of a given Minecraft server, Java or Bedrock.")]
    public async Task MinecraftCheck([Summary("address", "IP Address or URL of the server, optionally with port.")] string addr, [Summary("bedrock", "Specify 'true' if you're checking for a bedrock server.")] string bedrock = "false", [Summary("list-players", "Specify 'true' if you want a list of players.")] string listPlayers = "false")
    {
        string checkReq;
        if (bedrock == "false")
        {
            checkReq = $"https://api.mcsrvstat.us/2/{addr}";
        }
        else
        {
            checkReq = $"https://api.mcsrvstat.us/bedrock/2/{addr}";
        }

        var response = await Global.HttpClient.GetStringAsync(checkReq);

        var data = JsonConvert.DeserializeObject(response);
        if (data == null) return;
        dynamic dataObj = data;

        var dbUser = await UserEngine.GetDbUser(Context.User);

        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "Minecraft server status";
        embed.Description = addr;

        embed.Footer = new EmbedFooterBuilder() { Text = "data by api.mcsrvstat.us" };

        if (dataObj.online == "false")
        {
            embed.AddField("Status", "Server is offline.");
        }
        else
        {
            embed.AddField("Status", "Server is online");

            if (dataObj.players is not null)
                embed.AddField("Players", $"{dataObj.players.online}/{dataObj.players.max}", true);

            if (listPlayers != "false" && dataObj.players.list is not null)
            {
                string playerList = "";
                foreach (string item in dataObj.motd.clean)
                {
                    if (playerList == "")
                    {
                        playerList = item;
                    }
                    else
                    {
                        playerList += $"\n{item}";
                    }
                }
                embed.AddField("Player list", playerList);
            }

            embed.AddField("Version", dataObj.version, true);

            string serverName = "";
            foreach (string item in dataObj.motd.clean)
            {
                if (serverName == "")
                {
                    serverName = item;
                }
                else
                {
                    serverName += $" | {item}";
                }
            }
            embed.AddField("Name", serverName);

            if (dataObj.software is not null)
                embed.AddField("Software", dataObj.software, true);

            if (dataObj.plugins is not null && dataObj.plugins.raw is not null)
            {
                embed.AddField("Plugins", dataObj.plugins.raw.Count, true);
            }

            if (dataObj.mods is not null && dataObj.mods.raw is not null)
            {
                embed.AddField("Mods", dataObj.plugins.mods.Count, true);
            }
        }

        await RespondAsync(embed: embed.Build());
    }

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