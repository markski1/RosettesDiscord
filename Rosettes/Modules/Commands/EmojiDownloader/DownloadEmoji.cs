using Discord;
using Discord.Commands;
using Rosettes.Core;
using System.IO.Compression;

namespace Rosettes.Modules.Commands.EmojiDownloader
{
    public static class DownloadEmoji
    {
        private static IReadOnlyCollection<GuildEmote>? EmoteCollection;
        private static SocketCommandContext? ServerContext;
        private static bool IsDownloading = false;

        public static async Task DoTheThing(SocketCommandContext serverContext)
        {
            ServerContext = serverContext;
            EmoteCollection = await ServerContext.Guild.GetEmotesAsync();
            string ServerName = ServerContext.Guild.Name;
            int emoteAmount = EmoteCollection.Count;
            int progress = 0;
            IUserMessage messageId;

            if (emoteAmount < 1)
            {
                await ServerContext.Channel.SendMessageAsync("There are no custom emoji in this server, or I failed to retrieve them for some reason.");
                return;
            }
            else
            {
                await ServerContext.Channel.SendMessageAsync("I'll now download every emoji in this server, and I'll send it over as a ZIP when I'm done.");
                messageId = await ServerContext.Channel.SendMessageAsync($"Progress: `0/{emoteAmount}`");
            }
            IsDownloading = true;

            string fileName = "";
            string serverName = ServerContext.Guild.Name.Replace(" ", "");
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
            foreach (GuildEmote emote in EmoteCollection)
            {
                using (var stream = await Global.HttpClient.GetStreamAsync(emote.Url))
                {
                    if (emote.Animated)
                    {
                        fileName = $"./temp/{serverName}/{emote.Name}.gif";
                    }
                    else
                    {
                        fileName = $"./temp/{serverName}/{emote.Name}.png";
                    }
                    using var fileStream = new FileStream(fileName, FileMode.Create);
                    await stream.CopyToAsync(fileStream);
                }
                progress++;
                // update the message with the current progress, every 3rd emoji.
                if (progress % 3 == 0) await messageId.ModifyAsync(x => x.Content = $"Progress: `{progress}/{emoteAmount}`");
            }
            // done, update message.
            await messageId.ModifyAsync(x => x.Content = $"Progress: `Complete!`");
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
                if (File.Exists($"/var/www/html/{serverName}.zip"))
                {
                    File.Delete($"/var/www/html/{serverName}.zip");
                }
                File.Move(zipPath, $"/var/www/html/{serverName}.zip");

                await ServerContext.Channel.SendMessageAsync($"Done! The ZIP file with all emoji is now available at <https://snep.markski.ar/{serverName}.zip>.");
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("exportemoji", $"Error allocation zip file for emoji\n{ex.Message}");
                await ServerContext.Channel.SendMessageAsync($"Sorry! Emojis were exported, but there was an issue allocating the zip file.");
            }
            IsDownloading = false;
        }

        public static bool CheckIsDownloading()
        {
            return IsDownloading;
        }
    }
}
