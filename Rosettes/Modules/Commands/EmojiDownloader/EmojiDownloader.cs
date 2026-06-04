using Discord;
using Discord.Interactions;
using Rosettes.Core;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Rosettes.Modules.Commands.EmojiDownloader;

public partial class EmojiDownloader
{
    private IReadOnlyCollection<GuildEmote>? _emoteCollection;

    public async Task DownloadEmojis(SocketInteractionContext serverContext)
    {
        _emoteCollection = await serverContext.Guild.GetEmotesAsync();
        int emoteAmount = _emoteCollection.Count;
        int progress = 0;

        EmbedBuilder embed = await Global.MakeRosettesEmbed();

        embed.Title = "Exporting emoji.";

        EmbedFieldBuilder statusField = new() { Name = "Status" };

        embed.AddField(statusField);

        if (emoteAmount < 1)
        {
            await serverContext.Interaction.RespondAsync("There are no custom emoji in this server, or I failed to retrieve them for some reason.");
            return;
        }

        statusField.Value = $"Progress: `0/{emoteAmount}`";
        await serverContext.Interaction.RespondAsync(embed: embed.Build());

        string serverName = serverContext.Guild.Name.Replace(" ", "");
        Regex rgx = MyRegex();
        serverName = rgx.Replace(serverName, "");

        await using var zipStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (GuildEmote emote in _emoteCollection)
            {
                string entryName = emote.Animated ? $"{emote.Name}.gif" : $"{emote.Name}.png";
                var entry = zipArchive.CreateEntry(entryName, CompressionLevel.Fastest);

                await using var entryStream = entry.Open();
                await using var httpStream = await Global.HttpClient.GetStreamAsync(emote.Url);
                await httpStream.CopyToAsync(entryStream);

                progress++;

                if (progress % 3 != 0) continue;

                statusField.Value = $"Progress: `{progress}/{emoteAmount}`";
                try
                {
                    await serverContext.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
                }
                catch
                {
                    _ = Task.Run(async () => { await serverContext.User.SendMessageAsync("I don't have permission to send or edit messasges in that channel, can't complete emoji export."); });
                    return;
                }
            }
        }

        statusField.Value = "Progress: `Done exporting. Uploading, please wait...`";
        await serverContext.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());

        try
        {
            zipStream.Position = 0;
            string zipName = $"{serverName}.zip";
            await serverContext.Interaction.FollowupWithFileAsync(zipStream, zipName, text: "Here is your emoji export.");
        }
        catch (Exception ex)
        {
            statusField.Value = "Progress: `Failed. Sorry, the error has been reported.`";
            Global.GenerateErrorMessage("emojiExporter", $"{ex}");
            await serverContext.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
        }
    }

    [GeneratedRegex("[^a-zA-Z0-9 -]")]
    private static partial Regex MyRegex();
}
