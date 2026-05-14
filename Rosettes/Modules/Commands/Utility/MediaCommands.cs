using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
using Rosettes.Core;
using System.Text;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands.Utility;

[CommandContextType(
    InteractionContextType.BotDm,
    InteractionContextType.PrivateChannel,
    InteractionContextType.Guild
)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class MediaCommands : InteractionModuleBase<SocketInteractionContext>
{
    private sealed record CachedMedia(string MediaUri, string FileName);
    private static readonly Dictionary<string, CachedMedia> MediaCache = [];

    [SlashCommand("chat", "Chat with Rosettes")]
    public async Task Chat(string question)
    {
        await DeferAsync();

        if (question.Length > 1024)
        {
            await RespondAsync($"Sorry, please keep your question below 1024 characters. \n" +
                               $"For your convenience, here it is: ```{question}```");
        }
        
        ulong channelId;

        if (Context.Channel is null) channelId = 0;
        else channelId = Context.Channel.Id;

        var (isNewChat, success, response) = await LanguageEngine.GetResponseAsync(
                userId: Context.User.Id,
                channelId: channelId,
                message: question
            );

        if (success)
        {
            var dbUser = await UserEngine.GetDbUser(Context.User);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
            if (isNewChat) embed.Footer = new EmbedFooterBuilder { Text = "Use `/chat` to respond, or `/chat clear` and I'll forget this conversation." };
            embed.AddField("Question", question);
            if (response.Length < 1024)
            {
                embed.AddField("Answer", response);
                await FollowupAsync(embed: embed.Build());
            }
            else if (response.Length < 3000)
            {
                List<string> parts = SplitResponse(response);
                bool first = true;
                foreach (var part in parts)
                {
                    if (first)
                    {
                        embed.AddField("Answer", part);
                        first = false;
                    }
                    else embed.AddField("...", part);
                }
                await FollowupAsync(embed: embed.Build());
            }
            else
            {
                embed.AddField("Answer", "Response attached as file due to character count constraints.");

                var bytes = Encoding.UTF8.GetBytes(response);
                await using var ms = new MemoryStream(bytes);

                var fileName = $"response_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.txt";

                await FollowupWithFileAsync(
                    fileStream: ms,
                    fileName: fileName,
                    embed: embed.Build()
                );
            }
        }
        else
        {
            await FollowupAsync(response);
        }
    }

    
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
        if (uri.Contains("youtu.be") || uri.Contains("youtube.com"))
        {
            await DeclareDownloadFailure("Sorry, YouTube videos are currently not downloadable through Rosettes.");
            return;
        }

        uri = uri.Trim();

        string? mediaUri;
        string fileName;

        if (MediaCache.TryGetValue(uri, out var cached))
        {
            mediaUri = cached.MediaUri;
            fileName = cached.FileName;
        }
        else
        {
            string requestData = JsonConvert.SerializeObject(new { url = uri });

            HttpRequestMessage request = new(HttpMethod.Post, "http://127.0.0.1:9000");
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

            // In some cases, such as x.com posts, a direct URI might be unavailable, but there could be a picker.
            if (mediaUri is null)
            {
                if (responseData.pickerType is not null)
                {
                    if (responseData.pickerType == "images")
                    {
                        await DeclareDownloadFailure("This post seems to only contain images.");
                        return;
                    }

                    foreach (var item in responseData.picker)
                    {
                        if (item.type != "video") continue;
                        mediaUri = item.url;
                        baseName = item.filename;
                        break;
                    }
                }
                else
                {
                    await DeclareDownloadFailure("No media found in the post.");
                    return;
                }
            }

            if (mediaUri is null)
            {
                await DeclareDownloadFailure("No media found in the post.");
                return;
            }

            baseName ??= $"rosettes_{Global.Randomize(10000) + 1}.mp4";
            fileName = baseName;
            
            mediaUri = mediaUri.Replace("https://cobalt.markski.ar", "http://127.0.0.1:9000");

            // Cache resolved target so the next call skips the metadata request
            MediaCache[uri] = new CachedMedia(mediaUri, fileName);
        }

        ulong sizeLimit = Context.Guild?.MaxUploadLimit ?? 0;

        try
        {
            await using var ms = await DownloadToMemoryAsync(mediaUri, sizeLimit);
            ms.Position = 0;

            await FollowupWithFileAsync(
                fileStream: ms,
                fileName: fileName
            );
        }
        catch (InvalidOperationException ex)
        {
            await DeclareDownloadFailure(ex.Message, mediaUri);
        }
        catch
        {
            await DeclareDownloadFailure("Cannot upload file, likely too large.", mediaUri);
        }
    }

    private static async Task<MemoryStream> DownloadToMemoryAsync(string url, ulong sizeLimit)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Global.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Download failed. [{response.StatusCode}]");

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is not null && sizeLimit > 0 && (ulong)contentLength.Value > sizeLimit)
            throw new InvalidOperationException("Cannot upload file, too large.");

        await using var stream = await response.Content.ReadAsStreamAsync();

        var ms = new MemoryStream(
            capacity: contentLength is > 0 and <= int.MaxValue ? (int)contentLength.Value : 0
        );

        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            int read = await stream.ReadAsync(buffer);
            if (read <= 0) break;

            total += read;
            if (sizeLimit > 0 && (ulong)total > sizeLimit)
                throw new InvalidOperationException("Cannot upload file, too large.");

            await ms.WriteAsync(buffer.AsMemory(0, read));
        }

        return ms;
    }

    private async Task DeclareDownloadFailure(string message, string? mediaUri = null)
    {
        EmbedBuilder embed = await Global.MakeRosettesEmbed();

        embed.Title = "Video download failure.";

        EmbedFieldBuilder result = new() { Name = "Result", Value = message, IsInline = true };
        embed.AddField(result);

        if (mediaUri is not null && !mediaUri.Contains("127.0.0.1"))
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
    
    private static List<string> SplitResponse(string text)
    {
        List<string> parts = [];
        while (text.Length > 0)
        {
            if (text.Length <= 1024)
            {
                parts.Add(text);
                break;
            }
            int splitAt = FindSplitPoint(text, 1024);
            string part = text[..splitAt];
            parts.Add(part);
            text = text[splitAt..].TrimStart();
        }
        return parts;
    }

    private static int FindSplitPoint(string text, int maxLen)
    {
        int pos = maxLen;
        while (pos > 0)
        {
            char c = text[pos - 1];
            if (c is '.' or '\n')
            {
                return pos;
            }
            pos--;
        }
        return maxLen;
    }
}
