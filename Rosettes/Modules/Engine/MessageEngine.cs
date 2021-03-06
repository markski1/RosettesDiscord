using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Rosettes.Core;

namespace Rosettes.Modules.Engine
{
    public static class MessageEngine
    {
        private static readonly DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        public static async Task HandleMessage(SocketCommandContext context)
        {
            if (!NoMessageChannel(context)) return;
            var dbGuild = await GuildEngine.GetDBGuild(context.Guild);
            // if a guild's message analysis level is 0, don't process messages at all.
            if (dbGuild.MessageAnalysis() == 0)
            {
                return;
            }

            dbGuild.Messages++;
            _ = HandleExperience(context);

            // if a guild's message analysis level is 1, don't parse messages.
            if (dbGuild.MessageAnalysis() == 1) return;

            var message = context.Message;
            var messageText = message.Content.ToLower();

            if (messageText.Contains(@"store.steampowered.com/app/"))
            {
                await GetGameInfo(context);
                return;
            }
            // profile pattern disabled until get profile info does something
            if (/*messageText.Contains(@"steamcommunity.com/profiles/") || */messageText.Contains(@"steamcommunity.com/id/"))
            {
                await GetProfileInfo(context);
                return;
            }

            if (message.MentionedUsers.Any())
            {
                foreach (var user in message.MentionedUsers)
                {
                    if (user.Username.ToLower().Contains("rosettes"))
                    {
                        await context.Channel.SendMessageAsync("mew wew");
                    }
                }
            }
        }

        private static async Task HandleExperience(SocketCommandContext context)
        {
            var user = await UserEngine.GetDBUser(context.User);
            if (!user.IsValid()) return;
            if (context.Message.Attachments.Any())
            {
                user.AddExperience(2);
            }
            else
            {
                user.AddExperience(1);
            }
        }

        private static bool NoMessageChannel(SocketCommandContext context)
        {
            switch (context.Channel.GetChannelType())
            {
                case ChannelType.News:
                    return false;
                case ChannelType.NewsThread:
                    return false;
                case ChannelType.PublicThread:
                    return false;
                case ChannelType.PrivateThread:
                    return false;
                case ChannelType.DM:
                    return false;
            }
            if (context.Channel.Id == 924483284749156412) return false; // ignore a certain channel TODO: make this a database table.

            return true;
        }

        public static async Task GetGameInfo(SocketCommandContext context)
        {
            // Grab the game's ID from the url itself. It's located after '/app/' and sometimes the name is after it.
            string extractID = context.Message.Content;
            int begin = extractID.IndexOf("/app/") + 5;
            int end = -1;
            for (int i = begin; i < extractID.Length; i++)
            {
                if (char.IsNumber(extractID[i]))
                {
                    end = i + 1;
                    continue;
                }
                end = i;
                break;
            }
            if (end == -1) return;
            int gameID = int.Parse(extractID[begin..end]);
            string data;
            try
            {
                data = await Global.HttpClient.GetStringAsync($"https://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid={gameID}");
            }
            catch
            {
                // an exception will mean a 404 or steam being down, I think.
                // nothing to handle here.
                return;
            }

            var DeserialziedObject = JsonConvert.DeserializeObject(data);
            if (DeserialziedObject == null) return;
            dynamic result = ((dynamic)DeserialziedObject).response;

            if (result.result != 1) return;
            await context.Channel.SendMessageAsync($"^ Playing right now: {result.player_count:N0}");
        }

        public static async Task GetProfileInfo(SocketCommandContext context)
        {
            //extract steamID from url
            string extractID = context.Message.Content;
            ulong steamID;
            // easy mode: if it's a "profiles" url, just extract the number off the url
            if (extractID.Contains("/profiles/"))
            {
                int begin = extractID.IndexOf("/profiles/") + 10;
                int end = -1;
                for (int i = begin; i < extractID.Length; i++)
                {
                    if (char.IsNumber(extractID[i]))
                    {
                        end = i + 1;
                        continue;
                    }
                    end = i;
                    break;
                }
                if (end == -1) return;
                steamID = ulong.Parse(extractID[begin..end]);
            }
            // "hard" mode: if it's a vanity URL, resolve it through Steam WebAPI
            else
            {
                int begin = extractID.IndexOf("/id/") + 4;
                int end = -1;
                for (int i = begin; i < extractID.Length; i++)
                {
                    if (extractID[i] != '/')
                    {
                        continue;
                    }
                    end = i;
                    break;
                }
                if (end == -1) end = extractID.Length;
                string vanityURL;
                try
                {
                    vanityURL = extractID[begin..end];
                }
                catch
                {
                    // If substring results in an exception, the url wasn't valid
                    return;
                }
                var data = await Global.HttpClient.GetStringAsync($"https://api.steampowered.com/ISteamUser/ResolveVanityURL/v1/?key={Settings.SteamDevKey}&vanityurl={vanityURL}");

                var DeserialziedObject = JsonConvert.DeserializeObject(data);
                if (DeserialziedObject == null) return;
                dynamic result = ((dynamic)DeserialziedObject).response;

                if (result.success != 1) return;
                steamID = result.steamid;
            }
            await context.Channel.SendMessageAsync($"^ SteamID: {steamID}");
        }
    }
}