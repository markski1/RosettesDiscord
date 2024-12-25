using Discord;
using Discord.Interactions;
using Microsoft.AspNetCore.Routing.Constraints;
using Newtonsoft.Json;
using Rosettes.Core;
using System.Linq;
using System.Text;

namespace Rosettes.Modules.Commands.Utility;

[CommandContextType(
    InteractionContextType.BotDm,
    InteractionContextType.PrivateChannel,
    InteractionContextType.Guild
)]

[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class MediaCommands : InteractionModuleBase<SocketInteractionContext>
{
    public static Dictionary<string, string> MediaCache = [];

    [MessageCommand("Extract video")]
    public async Task GetVideoMsg(IMessage message)
    {
        string uri = Global.GrabUriFromText(message.Content);
        if (uri != "0")
        {
            await DeferAsync();
            await FetchMedia(uri);
        }
        else await RespondAsync("No URI found in this message.", ephemeral: true);
    }


    [SlashCommand("getvideo", "Get the video from a provided URI.")]
    public async Task GetVideo(string uri)
    {
        await DeferAsync();
        await FetchMedia(uri);
    }


    private async Task FetchMedia(string uri)
    {
        // store the file locally
        if (!Directory.Exists("./temp/media/"))
        {
            Directory.CreateDirectory("./temp/media/");
        }

        if (uri.Contains("youtu.be") || uri.Contains("youtube.com"))
        {
            await DeclareDownloadFailure("Sorry, YouTube videos are currently not downloadable through Rosettes.");
            return;
        }

        uri = uri.Trim();

        string fileName;
        string? mediaUri = null;

        if (MediaCache.ContainsKey(uri))
        {
            fileName = MediaCache[uri];
        }
        else
        {

            string requestData = JsonConvert.SerializeObject(
                new
                {
                    url = uri
                }
            );

            HttpRequestMessage request = new(HttpMethod.Post, "http://snep.vps.webdock.cloud:9000/");

            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(requestData, Encoding.UTF8);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;

            try
            {
                response = await Global.HttpClient.SendAsync(request);
            }
            catch
            {
                await DeclareDownloadFailure("Unable to connect, please try later.");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                await DeclareDownloadFailure($"Sorry, I was unable to obtain this video. [{response.StatusCode}]");
                return;
            }

            string responseContent = await response.Content.ReadAsStringAsync();
            dynamic? responseData = JsonConvert.DeserializeObject<dynamic>(responseContent);

            if (responseData is null)
            {
                await DeclareDownloadFailure("Failed to obtain media (URI might be invalid).");
                return;
            }

            mediaUri = responseData.url;
            string? baseName = responseData.filename;

            // In some cases, such as Tweets, a direct URI might be unavailable, but there could be a picker.
            if (mediaUri is null)
            {
                if (responseData.pickerType is not null)
                {
                    if (responseData.pickerType == "images")
                    {
                        await DeclareDownloadFailure("This tweet seems to only contain images.");
                        return;
                    }
                    else
                    {
                        foreach (var item in responseData.picker)
                        {
                            if (item.type == "video")
                            {
                                mediaUri = item.url;
                                baseName = item.filename;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    await DeclareDownloadFailure("No media found in the tweet.");
                    return;
                }
            }

            if (mediaUri is null)
            {
                await DeclareDownloadFailure("No media found in the tweet.");
                return;
            }

            bool cacheMedia = true;

            if (baseName is null)
            {
                cacheMedia = false;
                baseName ??= $"rosettes_{Global.Randomize(10000) + 1}.mp4";
            }

            fileName = $"./temp/media/rosettes_{Global.Randomize(99) + 1}_{baseName}";

            int seconds = 6;

            bool success = await Global.DownloadFile(fileName, mediaUri, seconds);

            if (!success)
            {
                await DeclareDownloadFailure("Error downloading the file, maybe it was too large.", mediaUri);
                return;
            }

            if (cacheMedia) MediaCache[uri] = fileName;
        }
            
        ulong size = (ulong)new FileInfo(fileName).Length;

        // check if the guild supports a file this large, otherwise fail.
        if (Context.Guild == null || Context.Guild.MaxUploadLimit > size)
        {
            try
            {
                await FollowupWithFileAsync(fileName);
            }
            catch
            {
                await DeclareDownloadFailure("Error uploading the file, maybe it was too large.", mediaUri);
            }
        }
        else
        {
            await DeclareDownloadFailure("File could not be uploaded, it is too large.", mediaUri);
        }
    }

    private async Task DeclareDownloadFailure(string message, string? mediaUri = null)
    {
        EmbedBuilder embed = await Global.MakeRosettesEmbed();

        embed.Title = "Video download failure.";

        EmbedFieldBuilder result = new() { Name = "Result", Value = message, IsInline = true };
        embed.AddField(result);

        if (mediaUri is not null)
        {
            embed.AddField("Instead...", $"Have a [Direct link]({mediaUri}).");
            await FollowupAsync(embed: embed.Build());
        }
        else
        {
            var msg = await FollowupAsync(embed: embed.Build());
            _ = new MessageDeleter(msg, 30);
        }
    }
}
