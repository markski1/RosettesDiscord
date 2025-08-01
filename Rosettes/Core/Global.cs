﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using JikanDotNet;
using PokeApiNet;
using Rosettes.Managers;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Minigame.Pets;

namespace Rosettes.Core;

public static class Global
{
    public static readonly RosettesMain RosettesMain = new();
    public static readonly HttpClient HttpClient = new();
    public static readonly PokeApiClient PokeClient = new();
    public static readonly Jikan Jikan = new();
    private static readonly Random Randomizer = new();

    public static int Randomize(int num)
    {
        return Randomizer.Next(num);
    }

    public static int Randomize(int minNum, int maxNum)
    {
        return Randomizer.Next(minNum, maxNum);
    }

    // percentage chances of returning true, otherwise false.
    public static bool Chance(int percentage)
    {
        return percentage > Randomizer.Next(100);
    }

    public static async Task<(bool, string)> DownloadFile(string path, string uri, ulong maxSize = 0)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var client = new HttpClient();
        
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            }
            catch
            {
                return (false, "Connection error");
            }

            if (!response.IsSuccessStatusCode)
            {
                return (false, "Unsuccesful response");
            }

            var fileSize = response.Content.Headers.ContentLength;
            
            if (maxSize > 0 && fileSize > (long?)maxSize)
            {
                return (false, "File is too large");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

            try
            {
                // Copy data in chunks, enforcing timeout
                await stream.CopyToAsync(fileStream, 81920, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return (false, "Opeartion took too long");
            }

            return (true, "");
        }
        catch
        {
            return (false, "Exception found");
        }
    }

    public static async Task<EmbedBuilder> MakeRosettesEmbed(User? dbUser = null)
    {
        EmbedBuilder embed = new()
        {
            Color = Color.DarkPurple
        };

        if (dbUser is not null)
        {
            var author = await dbUser.GetDiscordReference();

            EmbedAuthorBuilder authorEmbed = new();
            embed.Author = authorEmbed;

            authorEmbed.Name += $"{await dbUser.GetName()} [lv {dbUser.GetLevel()}]";
            authorEmbed.IconUrl = author?.GetDisplayAvatarUrl();

            var pet = await PetEngine.GetUserPet(dbUser);
            if (pet is not null)
            {
                authorEmbed.Name += $" [{pet.GetName()}]";
            }
        }

        // Transparent pixel
        embed.ThumbnailUrl = "https://markski.ar/images/trans.png";

        return embed;
    }

    public static void GenerateErrorMessage(string source, string error)
    {
        // generate the error string
        string errorText = $"There was an error at \"{source}\".\n```{error}```\n";

        if (errorText.Length > 1999)
        {
            errorText = $"{errorText[..1900]} ```(truncated)";
        }

        GenerateNotification(errorText);
    }

    public static void GenerateNotification(string message)
    {
        var client = ServiceManager.GetService<DiscordSocketClient>();
        if (client.GetChannel(984608927775854594) is not ITextChannel impawtantChannel) return;
        
        Console.WriteLine(message);
        impawtantChannel.SendMessageAsync(message);
    }

    public static int CurrentUnix()
    {
        return (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    }

    public static string GrabUriFromText(string text)
    {
        // try to grab the first URL from the received text.
        // Start by finding the first instance of http, and end as soon as we find a space or a control character.
        // return "0" if we can't find a url.
        var begin = text.IndexOf("https:/", StringComparison.Ordinal);
        if (begin == -1)
        {
            begin = text.IndexOf("http:/", StringComparison.Ordinal);
            if (begin == -1) return "0";
        }

        var end = text
            .Skip(begin)
            .TakeWhile(x => !(char.IsWhiteSpace(x) || char.IsControl(x)))
            .Count() + begin;

        string url = text[begin..end];
        //remove anti-embed artifact
        url = url.Replace(">", string.Empty);
        return url;
    }
    
    public static bool CheckIsEmoteOrEmoji(string anEmoji)
    {
        try
        {
            Emote.Parse(anEmoji);
            return true;
        }
        catch
        {
            try
            {
                Emoji.Parse(anEmoji);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static ulong GetSelfId()
    {
    #if DEBUG
        return 815231883944263681;
    #else
        return 970176524110147605;
    #endif
    }

    private static SocketGuildUser GetSelfGuildUser(SocketGuild guild)
    {
        return guild.GetUser(GetSelfId());
    }

    public static bool CanSendMessage(SocketInteractionContext context)
    {
        if (context.Channel is null) return false;
        
        var selfUser = GetSelfGuildUser(context.Guild);
        var access = selfUser.GetPermissions(context.Channel as IGuildChannel);

        return access.SendMessages;
    }
}

public class MessageDeleter
{
    private readonly System.Timers.Timer _timer = new();
    private readonly IMessage _message;

    public MessageDeleter(IMessage message, int seconds)
    {
        _timer.Elapsed += DeleteMessage;
        _timer.Interval = seconds * 1000;
        _message = message;
        _timer.Enabled = true;
    }

    private void DeleteMessage(object? source, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            _message.DeleteAsync();
        }
        catch
        {
            // nothing to do if lacking perms at this point, just don't crash.
        }
        _timer.Stop();
        _timer.Dispose();
    }
}