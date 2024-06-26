﻿using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
using Rosettes.Core;
using System.Net.Http;
using System.Security.Cryptography;
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

        EmbedFieldBuilder downloadStatus = new() { Name = "Status", Value = "Obtaining URI information.", IsInline = true };

        embed.AddField(downloadStatus);

        IUserMessage? mid;
        
        // The 'middle state' message may not be created if the bot has no perms.
        try
        {
            mid = await ReplyAsync(embed: embed.Build());
        }
        catch
        {
            mid = null;
        }

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

        if (mid is not null)
        {
            downloadStatus.Value = "Downloading...";
            await mid.ModifyAsync(x => x.Embed = embed.Build());
        }

        string MediaURI = responseData.url;

        string fileExt = type == "video" ? "mp4" : "mp3";
        string fileName = $"./temp/media/{Global.Randomize(20) + 1}.{fileExt}";

        bool success = await Global.DownloadFile(fileName, MediaURI);

        if (!success)
        {
            await DeclareDownloadFailure(downloadStatus, mid, embed, "Error downloading the file, maybe it was too large.", MediaURI);
            return;
        }

        if (mid is not null)
        {
            downloadStatus.Value = "Uploading to Discord...";
            await mid.ModifyAsync(x => x.Embed = embed.Build());
        }
        
        ulong size = (ulong)new FileInfo(fileName).Length;

        // check if the guild supports a file this large, otherwise fail.
        if (Context.Guild == null || Context.Guild.MaxUploadLimit > size)
        {
            try
            {
                await FollowupWithFileAsync(fileName);
                if (mid is not null) await mid.DeleteAsync();
            }
            catch
            {
                downloadStatus.Value = "Upload failed.";

                await FollowupAsync(embed: embed.Build());
                if (mid is not null) await mid.DeleteAsync();
            }
        }
        else
        {
            downloadStatus.Value = "Upload failed. File was too large.";
            embed.AddField("Instead...", $"Have a [Direct link]({MediaURI}).");
            await FollowupAsync(embed: embed.Build());
            if (mid is not null) await mid.DeleteAsync();
        }
        File.Delete(fileName);
    }

    private async Task<Task> DeclareDownloadFailure(EmbedFieldBuilder downloadStatus, IUserMessage? mid, EmbedBuilder embed, string message, string? MediaURI = null)
    {
        downloadStatus.Value = message;

        if (mid is not null)
        {
            await mid.DeleteAsync();
        }

        if (MediaURI is not null)
        {
            embed.AddField("Instead...", $"Have a [Direct link]({MediaURI}).");
        }

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
