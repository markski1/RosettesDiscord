using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Guild;

namespace Rosettes.Managers;

public static class MessageManager
{
    public static async Task HandleMessage(SocketCommandContext context)
    {
        if (!NoMessageChannel(context)) return;
        TelemetryEngine.Count(TelemetryType.Message);
        var dbGuild = await GuildEngine.GetDBGuild(context.Guild);
        // if a guild's message analysis level is 0, don't parse messages at all.
        if (!dbGuild.MessageAnalysis())
        {
            return;
        }

        var message = context.Message;
        var messageText = message.Content.ToLower();

        if (messageText.Contains(@"store.steampowered.com/app/"))
        {
            await GetGameInfo(context);
            return;
        }
        // profile pattern disabled until get profile info does something
        if (messageText.Contains(@"steamcommunity.com/profiles/") || messageText.Contains(@"steamcommunity.com/id/"))
        {
            await GetProfileInfo(context);
            return;
        }

        if (messageText.Contains(@"i.4cdn.org"))
        {
            await MirrorExpiringMedia(context);
        }
    }

    private static bool NoMessageChannel(SocketCommandContext context)
    {
        return context.Channel.GetChannelType() switch
        {
            ChannelType.News => false,
            ChannelType.NewsThread => false,
            ChannelType.PrivateThread => false,
            ChannelType.DM => false,
            _ => true,
        };
    }

    private static async Task GetGameInfo(SocketCommandContext context)
    {
        // Grab the game's ID from the uri. It's located after '/app/' and sometimes the name is after it.
        string extractId = context.Message.Content;

        int begin = extractId.IndexOf("/app/", StringComparison.Ordinal) + 5; // where the number starts in the string.
        int end = -1;

        end = extractId
            .Skip(begin)
            .TakeWhile(char.IsNumber)
            .Count() + begin;                       // where it ends.

        if (end <= begin) return;

        int gameId = int.Parse(extractId[begin..end]);

        string data;
        try
        {
            data = await Global.HttpClient.GetStringAsync($"https://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid={gameId}");
        }
        catch
        {
            // an exception will mean a 404 or timeout.
            // nothing to handle here.
            return;
        }

        var deserialziedObject = JsonConvert.DeserializeObject(data);
        if (deserialziedObject == null) return;
        dynamic result = ((dynamic)deserialziedObject).response;

        if (result.result != 1) return;
        await context.Channel.SendMessageAsync($"^ Playing right now: {result.player_count:N0}");
    }
    private static async Task MirrorExpiringMedia(SocketCommandContext context)
    {
        string message = context.Message.Content;
        if (message is null) return;


        string uri = Global.GrabUriFromText(message);

        // Infer the format from the filename
        // TODO: Infer the format from the downloaded data instead.
        int formatBegin = uri.LastIndexOf('.');
        if (formatBegin == -1) return;
        string format = uri[formatBegin..uri.Length];

        // If the length of the grabbed "format" is too long, we can assume the URI didn't specify a format.
        if (format.Length > 5) return;

        var expireMessage = await context.Channel.SendMessageAsync("This URI will expire. I will now attempt to mirror it's media by uploading it directly to Discord.");

        try
        {
            var data = await Global.HttpClient.GetStreamAsync(uri);

            if (!Directory.Exists("./temp/")) Directory.CreateDirectory("./temp/");
            string fileName = $"./temp/{Global.Randomize(20) + 1}.{format}";

            if (File.Exists(fileName)) File.Delete(fileName);

            await using var fileStream = new FileStream(fileName, FileMode.Create);
            await data.CopyToAsync(fileStream);
            fileStream.Close();

            ulong size = (ulong)new FileInfo(fileName).Length;

            if (context.Guild.MaxUploadLimit > size)
            {
                await context.Channel.SendFileAsync(fileName);
            }
            else
            {
                throw new Exception("File was too large.");
            }

            File.Delete(fileName);
        }
        catch (Exception ex)
        {
            await expireMessage.ModifyAsync(x => x.Content = $"Sorry! I couldn't do it... - ({ex.Message})");
        }
    }

    private static async Task GetProfileInfo(SocketCommandContext context)
    {
        //extract steamID from uri
        string extractId = Global.GrabUriFromText(context.Message.Content);
        ulong steamId;
        // easy mode: if it's a "profiles" uri, just extract the number off the uri
        if (extractId.Contains("/profiles/"))
        {
            int begin = extractId.IndexOf("/profiles/", StringComparison.Ordinal) + 10;

            int end = -1;

            end = extractId
                .Skip(begin)
                .TakeWhile(char.IsNumber)
                .Count() + begin;                       // where it ends.

            if (end <= begin) return;

            steamId = ulong.Parse(extractId[begin..end]);
        }
        // "hard" mode: if it's a vanity URI, resolve it through Steam WebAPI
        else
        {
            int begin = extractId.IndexOf("/id/", StringComparison.Ordinal) + 4;

            int end = extractId
                .Skip(begin)
                .TakeWhile(x => x != '/')
                .Count() + begin;                       // stop where '/' found, if any.

            string vanityUri;
            try
            {
                vanityUri = extractId[begin..end];
            }
            catch
            {
                // If substring results in an exception, the uri wasn't valid
                return;
            }
            var data = await Global.HttpClient.GetStringAsync($"https://api.steampowered.com/ISteamUser/ResolveVanityURL/v1/?key={Settings.SteamDevKey}&vanityurl={vanityUri}");

            var deserialziedObject = JsonConvert.DeserializeObject(data);
            if (deserialziedObject == null) return;
            dynamic result = ((dynamic)deserialziedObject).response;

            if (result.success != 1) return;
            steamId = result.steamid;
        }

        var moreData = await Global.HttpClient.GetStringAsync($"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={Settings.SteamDevKey}&steamids={steamId}");

        var deserializedMoreData = JsonConvert.DeserializeObject(moreData);
        if (deserializedMoreData == null) return;
        dynamic moreDataObj = ((dynamic)deserializedMoreData).response.players;

        dynamic? player = null;

        // only one player in the object, but this is an easier way to extract as json deserialized objects do not support LINQ
        foreach (var extractPlayer in moreDataObj)
        {
            player = extractPlayer;
        }

        if (player is null) return;

        EmbedBuilder embed = await Global.MakeRosettesEmbed();
        embed.Author = new EmbedAuthorBuilder() { Name = player.personaname, IconUrl = player.avatar };
        embed.Description = $"Steam ID: {steamId}";

        if (player.communityvisibilitystate is not null)
        {
            int switchElement = int.Parse((string)player.communityvisibilitystate);
            string visibility = switchElement switch
            {
                3 => "Public",
                _ => "Private",
            };
            embed.AddField("Profile visibility", visibility, true);
        }

        if (player.personastate is not null)
        {
            int switchElement = int.Parse((string)player.personastate);
            string state = switchElement switch
            {
                0 => "Offline (or private)",
                1 => "Online",
                2 => "Busy",
                3 => "Away",
                4 => "Snooze",
                5 => "Looking to trade",
                6 => "Looking to play",
                _ => "Unknown",
            };

            embed.AddField("Status", state, true);
        }

        if (player.gameextrainfo is not null)
        {
            string text = player.gameextrainfo;
            if (player.gameserverip is not null)
            {
                if (player.gameserverip != "0.0.0.0:0") text += $"\nServer IP: {player.gameserverip}";
            }
            embed.AddField("Playing game", text);
        }
        else
        {
            embed.AddField("Playing game", "Not playing");
        }

        if (player.lastlogoff is not null)
        {
            embed.AddField("Last seen", $"<t:{player.lastlogoff}:f>", true);
        }

        if (player.timecreated is not null)
        {
            embed.AddField("Account created", $"<t:{player.timecreated}:f>", true);
        }


        await context.Channel.SendMessageAsync(embed: embed.Build());
    }
}