using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
using Rosettes.Core;
using System.Text;

namespace Rosettes.Modules.Commands.Utility;

public class MediaCommands : InteractionModuleBase<SocketInteractionContext>
{
    [MessageCommand("Extract video")]
    public async Task GetVideoMsg(IMessage message)
    {
        string uri = Global.GrabUriFromText(message.Content);
        if (uri != "0")
        {
            await DeferAsync();
            await FetchMedia(uri, "video");
        }
        else await RespondAsync("No URI found in this message.", ephemeral: true);
    }


    [SlashCommand("getvideo", "Get the video from a provided URI.")]
    public async Task GetVideo(string URI)
    {
        await DeferAsync();
        await FetchMedia(URI, "video");
    }


    [SlashCommand("getaudio", "Get the audio from a provided URI.")]
    public async Task GetAudio(string URI)
    {
        await DeferAsync();
        await FetchMedia(URI, "audio");
    }


    private async Task FetchMedia(string URI, string type)
    {
        EmbedBuilder embed = await Global.MakeRosettesEmbed();

        embed.Title = $"Fetching {type}.";

        EmbedFieldBuilder downloadStatus = new() { Name = "Status", Value = "Downloading...", IsInline = true };

        embed.AddField(downloadStatus);

        var mid = await ReplyAsync(embed: embed.Build());

        // store the file locally
        if (!Directory.Exists("./temp/media/"))
        {
            Directory.CreateDirectory("./temp/media/");
        }

        string requestData = JsonConvert.SerializeObject(
            new
            {
                url = URI,
                isAudioOnly = type == "audio"
            }
        );

        HttpRequestMessage request = new(HttpMethod.Post, "https://api.cobalt.tools/api/json");

        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(requestData, Encoding.UTF8);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        HttpResponseMessage response = await Global.HttpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            await DeclareDownloadFailure(downloadStatus, mid, embed, "Sorry, I was unable to obtain this video.");
            return;
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        dynamic? responseData = JsonConvert.DeserializeObject<dynamic>(responseContent);

        if (responseData is null || responseData.url is null)
        {
            await DeclareDownloadFailure(downloadStatus, mid, embed, "Failed to obtain media (URI might be invalid).");
            Console.WriteLine(responseContent);
            return;
        }

        string MediaURI = responseData.url;

        string fileExt = type == "video" ? "mp4" : "mp3";
        string fileName = $"./temp/media/{Global.Randomize(20) + 1}.{fileExt}";

        using (response = await Global.HttpClient.GetAsync(MediaURI))
        {
            if (!response.IsSuccessStatusCode)
            {
                await DeclareDownloadFailure(downloadStatus, mid, embed, "Failed to obtain media (Likely network failure).");
                return;
            }

            Stream stream = await response.Content.ReadAsStreamAsync();

            await using var fileStream = new FileStream(fileName, FileMode.Create);

            var cts = new CancellationTokenSource();
            var downloadTask = stream.CopyToAsync(fileStream, cts.Token);

            // cancel if media takes more than 5 seconds to download.
            if (await Task.WhenAny(downloadTask, Task.Delay(5000)) == downloadTask)
            {
                await downloadTask;
            }
            else
            {
                await DeclareDownloadFailure(downloadStatus, mid, embed, "Failed. Media took too long to download.");
                cts.Cancel();
                return;
            }
        }

        downloadStatus.Value = "Uploading to Discord...";
        await mid.ModifyAsync(x => x.Embed = embed.Build());

        ulong size = (ulong)new FileInfo(fileName).Length;

        // check if the guild supports a file this large, otherwise fail.
        if (Context.Guild == null || Context.Guild.MaxUploadLimit > size)
        {
            try
            {
                await FollowupWithFileAsync(fileName);
                _ = mid.DeleteAsync();
            }
            catch
            {
                downloadStatus.Value = "Upload failed.";

                await mid.DeleteAsync();
                await FollowupAsync(embed: embed.Build());
            }
        }
        else
        {
            downloadStatus.Value = "Upload failed. File was too large.";
            embed.AddField("Instead...", $"Have a [Direct link]({MediaURI}).");
            await FollowupAsync(embed: embed.Build());
            _ = mid.DeleteAsync();
        }
        File.Delete(fileName);
    }

    private async Task<Task> DeclareDownloadFailure(EmbedFieldBuilder downloadStatus, IUserMessage mid, EmbedBuilder embed, string message)
    {
        downloadStatus.Value = message;

        await mid.DeleteAsync();
        await FollowupAsync(embed: embed.Build());

        return Task.CompletedTask;
    }

    // Old, deprecated commands.


    [SlashCommand("twtvid", "Deprecated, use /getvideo")]
    public async Task TweetVideo()
    {
        await RespondAsync("This command is deprecated. You may now use /getvideo to extract a video from most websites!", ephemeral: true);
    }

    [SlashCommand("tikvid", "Get the video file of the specified TikTok post.")]
    public async Task TiktokVideo()
    {
        await RespondAsync("This command is deprecated. You may now use /getvideo to extract a video from most websites!", ephemeral: true);
    }
}
