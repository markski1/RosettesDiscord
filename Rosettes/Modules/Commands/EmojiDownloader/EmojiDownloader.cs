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

        // clean up servername
        string serverName = serverContext.Guild.Name.Replace(" ", "");
        Regex rgx = MyRegex();
        serverName = rgx.Replace(serverName, "");

        // ensure the folders to store the emoji exist.
        if (!Directory.Exists("./temp/"))
        {
            Directory.CreateDirectory("./temp/");
        }
        if (!Directory.Exists($"./temp/{serverName}/"))
        {
            Directory.CreateDirectory($"./temp/{serverName}/");
        }
        // download every emoji into this folder.
        foreach (GuildEmote emote in _emoteCollection)
        {
            await using (var stream = await Global.HttpClient.GetStreamAsync(emote.Url))
            {
                string fileName;
                if (emote.Animated)
                {
                    fileName = $"./temp/{serverName}/{emote.Name}.gif";
                }
                else
                {
                    fileName = $"./temp/{serverName}/{emote.Name}.png";
                }

                await using var fileStream = new FileStream(fileName, FileMode.Create);
                await stream.CopyToAsync(fileStream);
            }
            progress++;
            
            // update the message with the current progress, every 3rd emoji.
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
        // done, update message.
        statusField.Value = "Progress: `Done exporting. Compressing and uploading, please wait...`";
        await serverContext.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
        try
        {
            // zip up the file.
            string zipPath = $"./temp/{serverName}.zip";
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            ZipFile.CreateFromDirectory($"./temp/{serverName}", zipPath);

            // move the zip file to the webserver.
            if (File.Exists($"/var/www/html/downloads/{serverName}.zip"))
            {
                File.Delete($"/var/www/html/downloads/{serverName}.zip");
            }
            File.Move(zipPath, $"/var/www/html/downloads/{serverName}.zip");

            statusField.Value = "Progress: `Done exporting. Done uploading.`";
            embed.AddField("Download link", $"<https://rosettes.markski.ar/downloads/{serverName}.zip>");
            await serverContext.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
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
