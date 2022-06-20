using Discord.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rosettes.Core;

namespace Rosettes.Modules.Commands
{
    [Summary("Commands related to games.")]
    public class GameCommands : ModuleBase<SocketCommandContext>
    {
        [Command("csgo")]
        [Summary("Status of Steam and CSGO matchmaking.")]
        public async Task CSGOStatusAsync()
        {
            string text;
            try
            {
                var data = await Global.HttpClient.GetStringAsync($"https://api.steampowered.com/ICSGOServers_730/GetGameServersStatus/v1/?key={Settings.SteamDevKey}");

                var DeserialziedObject = JsonConvert.DeserializeObject(data);
                if (DeserialziedObject == null)
                {
                    await ReplyAsync("Failed to retrieve status data.");
                    return;
                }
                dynamic result = ((dynamic)DeserialziedObject).result;

                TimeSpan WaitTime = TimeSpan.FromSeconds(Convert.ToDouble(result.matchmaking.search_seconds_avg));

                text =
                    $"```\n" +
                    $"Steam Status:\n" +
                    $"================\n" +
                    $"Logon Service   : {result.services.SessionsLogon}\n" +
                    $"Steam Community : {result.services.SteamCommunity}\n" +
                    $"================\n" +
                    $"\n" +
                    $"CSGO Status:\n" +
                    $"================\n" +
                    $"Matchmaking     : {result.matchmaking.scheduler}\n" +
                    $"Online players  : {result.matchmaking.online_players:N0}\n" +
                    $"Online servers  : {result.matchmaking.online_servers:N0}\n" +
                    $"Searching game  : {result.matchmaking.searching_players:N0}\n" +
                    $"Average wait    : {WaitTime.Minutes} minute{((WaitTime.Minutes != 1) ? 's' : null)}, {WaitTime.Seconds} seconds.\n" +
                    $"================\n" +
                    $"```";
            }
            catch
            {
                text = "Failed to fetch status data. This might mean steam is down.";
            }
            await ReplyAsync(text);
        }

        [Command("ffxiv")]
        [Summary("Status of FFXIV servers.")]
        public async Task XIVSearchAsync(string checkServer = "NOTSPECIFIED")
        {
            string lobbyData;
            try
            {
                lobbyData = await Global.HttpClient.GetStringAsync("http://frontier.ffxiv.com/worldStatus/gate_status.json");
            }
            catch
            {
                await ReplyAsync("Failed to retrieve status data.");
                return;
            }
            var DeserializedLobbyObject = JsonConvert.DeserializeObject(lobbyData);
            if (DeserializedLobbyObject == null)
            {
                return;
            }

            dynamic lobby = (dynamic)DeserializedLobbyObject;

            string lobbyText = $"Lobby status    : {((lobby.status == 1) ? "Online" : "Offline")}";
            string serverText = "";
            if (checkServer == "NOTSPECIFIED")
            {
                serverText = $"For world status, please specify a datacenter name. ({Settings.Prefix}ffxiv <name>)\n";
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
                    await ReplyAsync("Failed to retrieve datacenter data.");
                    return;
                }

                var datacenterObj = JObject.Parse(datacenterData);
                var worldObj = JObject.Parse(worldData);
                if (datacenterObj == null || worldObj == null)
                {
                    await ReplyAsync("Failed to retrieve datacenter data.");
                    return;
                }

                string searchTerm = checkServer.ToLower();
                List<string> serverNames = new();

                foreach (var datacenter in datacenterObj.Cast<KeyValuePair<string, JToken>>().ToList())
                {
                    if (datacenter.Key.ToLower() == searchTerm)
                    {
                        foreach(var server in datacenter.Value)
                        {
                            serverNames.Add(server.ToString());
                        }
                    }
                }
                if (!serverNames.Any())
                {
                    serverText = "The specified datacenter was not found.\n";
                }
                else
                {
                    foreach (var world in worldObj.Cast<KeyValuePair<string, JToken>>().ToList())
                    {
                        if (serverNames.Contains(world.Key))
                        {
                            
                            int spacing = 16 - world.Key.ToString().Length;
                            string spacingText = "";
                            for (int i = 0; i < spacing; i++)
                            {
                                spacingText += " ";
                            }
                            serverText += $"{world.Key}{spacingText}: { (((int)world.Value == 1) ? "Online" : "Offline")}\n";
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
            await ReplyAsync(text);
        }
    }
}