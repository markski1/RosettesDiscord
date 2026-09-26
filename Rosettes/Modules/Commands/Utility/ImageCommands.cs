using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Text.Encodings.Web;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using Point = SixLabors.ImageSharp.Point;

namespace Rosettes.Modules.Commands.Utility;

[Group("image", "Image manipulation commands")]
public class ImageCommands : InteractionModuleBase<SocketInteractionContext>
{
    private const int MaxConversionBytes = 10 * 1024 * 1024;
    private const long MaxConversionPixels = 25_000_000;
    private static readonly SemaphoreSlim ConversionSlots = new(2, 2);

    [MessageCommand("SauceNAO Search")]
    public async Task SauceNaoCtx(IMessage message)
    {
        string? getUri = Global.GrabUriFromText(message.Content);

        // first try to find any image attached
        if (message.Attachments.Count != 0)
        {
            string fileType = message.Attachments.First().ContentType.ToLower();
            if (fileType.Contains("image/"))
            {
                getUri = message.Attachments.First().ProxyUrl;
            }
        }

        // if still no luck, try to grab an emote.
        if (getUri is null)
        {
            try
            {
                Emote emote = Emote.Parse(message.Content);
                getUri = emote.Url;
            }
            catch
            {
                await RespondAsync("No images or emotes found in this message.", ephemeral: true);
                return;
            }
        }

        getUri = UrlEncoder.Default.Encode(getUri);

        await SauceNao(getUri);
    }

    [SlashCommand("saucenao", "Use SauceNAO to try and find the source of a provided image url.")]
    public async Task SauceNao(string url)
    {
        string getUrl = $"https://saucenao.com/search.php?output_type=2&api_key={Settings.SauceNao}&url={url}";

        await DeferAsync();

        string response;

        try
        {
            response = await Global.HttpClient.GetStringAsync(getUrl);
        }
        catch
        {
            await FollowupAsync("Sorry, there was an error reaching the SauceNAO API. [SE1]");
            return;
        }

        var deserializedResponse = JsonConvert.DeserializeObject(response);

        if (deserializedResponse is null)
        {
            await FollowupAsync("Sorry, there was an error reaching the SauceNAO API. [SE2]");
            return;
        }

        dynamic responseObj = deserializedResponse;

        var dbUser = await UserEngine.GetDbUser(Context.User);

        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "SauceNAO Top Result";

        bool found = false;

        if (responseObj.results is null)
        {
            if (responseObj.header.short_remaining < 1)
            {
                embed.Description = "We are currently rate-limited by SauceNAO. This usually fixes itself after a minute or two, so try again in a bit, or see the results directly on SauceNAO.";
            }
            else
            {
                embed.Description = "Could not find matches or data on the requested image.";
            }
            
        }
        else
        {
            // due to the requests, responseObj.results will only have the top result and nothing else.
            // foreach is just a lazy way to check wether there is a result and to extract it.
            foreach (var item in responseObj.results)
            {
                found = true;

                string auxIndexName = item.header.index_name;

                // SauceNao NSFW indexes seem to largely begin with 'H-' then a denomination.
                if (auxIndexName.Contains("H-"))
                {
                    embed.AddField("Warning", "Result is potentially NSFW.\nThumbnail omitted.");
                }
                else
                {
                    embed.ThumbnailUrl = item.header.thumbnail;
                }

                embed.AddField("Similarity", $"{item.header.similarity} percent");
                string sources = "";
                if (item.data.ext_urls is not null)
                {
                    foreach (var src in item.data.ext_urls)
                    {
                        sources += $"{src}\n";
                    }
                }
                else
                {
                    sources = "No sources available.";
                }
                embed.AddField("Source URLs", sources);
            }

            if (!found)
            {
                await FollowupAsync("No results.");
                return;
            }
        }

        ComponentBuilder comps = new();

        comps.WithButton("See results on SauceNAO", style: ButtonStyle.Link, url: $"https://saucenao.com/search.php?url={url}");

        await FollowupAsync(embed: embed.Build(), components: comps.Build());
    }
    
    [MessageCommand("Add speech bubble")]
    public async Task CtxAddBubble(IMessage message)
    {
        string? getUri = Global.GrabUriFromText(message.Content);
        string cntType = "invalid";
        
        // try to find any image attached
        if (message.Attachments.Count != 0)
        {
            string fileType = message.Attachments.First().ContentType.ToLower();
            if (fileType.Contains("image/"))
            {
                getUri = message.Attachments.First().ProxyUrl;
                cntType = fileType;
            }
        }

        if (getUri is null)
        {
            await RespondAsync("No images found in this message.", ephemeral: true);
            return;
        }
        
        if (getUri.Contains(".mp4"))
        {
            await RespondAsync("Sorry, this seems to be encoded as an MP4 file, I cannot work with these.", ephemeral: true);
            return;       
        }
       
        await BubbleImage.CreateBubbleImage(Context, getUri, new Uri(getUri).LocalPath, cntType, false, false);
    }
    
    [IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
    [SlashCommand("bubble", "Add a bubble overlay on an image.")]
    public async Task CmdBubble(
        [Summary("image", "Attached image to be used.")] IAttachment image,
        [Summary("down", "Wether the bubble should aim down")] bool down = false,
        [Summary("left", "Wether the bubble should aim left")] bool left = false
    )
    {
        await BubbleImage.CreateBubbleImage(Context, image.Url, image.Filename, image.ContentType, down, left);
    }

    [IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
    [SlashCommand("convert", "Convert an image to PNG, JPEG, or WebP.")]
    public async Task ConvertImage(
        [Summary("image", "Image to convert.")] IAttachment image,
        [Summary("format", "Output image format.")]
        [Choice("PNG", "png")]
        [Choice("JPEG", "jpg")]
        [Choice("WebP", "webp")]
        string format)
    {
        if (format is not ("png" or "jpg" or "webp"))
        {
            await RespondAsync("Choose PNG, JPEG, or WebP.", ephemeral: true);
            return;
        }

        await DeferAsync();

        if (image.Size > MaxConversionBytes)
        {
            await FollowupAsync("Please attach an image smaller than 10 MB.");
            return;
        }

        if (!await ConversionSlots.WaitAsync(TimeSpan.FromSeconds(10)))
        {
            await FollowupAsync("Image conversion is busy right now. Please try again shortly.");
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), $"rosettes-convert-{Guid.NewGuid():N}.{format}");
        try
        {
            using var downloadTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await Global.HttpClient.GetAsync(image.Url, HttpCompletionOption.ResponseHeadersRead, downloadTimeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxConversionBytes)
            {
                await FollowupAsync("Please attach an image smaller than 10 MB.");
                return;
            }

            await using var input = await response.Content.ReadAsStreamAsync(downloadTimeout.Token);
            using var bytes = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, downloadTimeout.Token)) != 0)
            {
                if (bytes.Length + read > MaxConversionBytes)
                {
                    await FollowupAsync("Please attach an image smaller than 10 MB.");
                    return;
                }

                await bytes.WriteAsync(buffer.AsMemory(0, read), downloadTimeout.Token);
            }

            bytes.Position = 0;
            var info = SixLabors.ImageSharp.Image.Identify(new DecoderOptions { SkipMetadata = true }, bytes);
            if (info is null)
            {
                await FollowupAsync("I couldn't read that image format.");
                return;
            }

            if ((long)info.Width * info.Height > MaxConversionPixels)
            {
                await FollowupAsync("That image is too large to convert.");
                return;
            }

            bytes.Position = 0;
            using var conversionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using (var decoded = await SixLabors.ImageSharp.Image.LoadAsync(
                new DecoderOptions { MaxFrames = 1 }, bytes, conversionTimeout.Token))
            {
                decoded.Mutate(context => context.AutoOrient());
                if (format == "jpg")
                    decoded.Mutate(context => context.BackgroundColor(SixLabors.ImageSharp.Color.White));

                IImageEncoder encoder = format switch
                {
                    "png" => new PngEncoder { SkipMetadata = true },
                    "jpg" => new JpegEncoder { Quality = 85, SkipMetadata = true },
                    "webp" => new WebpEncoder { Quality = 85, SkipMetadata = true },
                    _ => throw new ArgumentOutOfRangeException(nameof(format))
                };
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await decoded.SaveAsync(output, encoder, conversionTimeout.Token);
            }

            ulong uploadLimit = (ulong)Context.Interaction.AttachmentSizeLimit;
            if (uploadLimit == 0) uploadLimit = MaxConversionBytes;
            if ((ulong)new FileInfo(path).Length > uploadLimit)
            {
                await FollowupAsync(format == "png"
                    ? "The converted image exceeds Discord's upload limit. Try JPEG or WebP for a smaller file."
                    : "The converted image exceeds Discord's upload limit. Try a smaller source image.");
                return;
            }

            await using var converted = File.OpenRead(path);
            await FollowupWithFileAsync(
                converted,
                $"converted.{format}",
                text: "Animated images are converted using their first frame.");
        }
        catch (OperationCanceledException)
        {
            await FollowupAsync("Image conversion timed out. Please try a smaller image.");
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        {
            await FollowupAsync("I couldn't read that image format.");
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("image-convert", ex.ToString());
            await FollowupAsync("Sorry, I couldn't convert that image.");
        }
        finally
        {
            ConversionSlots.Release();
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Global.GenerateErrorMessage("image-convert", $"Could not delete temporary image: {ex.Message}");
            }
        }
    }
}

public static class BubbleImage
{
    public static async Task CreateBubbleImage(SocketInteractionContext ctx, string imageUri, string imageName, string cntType, bool down, bool left)
    {
        if (!cntType.StartsWith("image/"))
        {
            await ctx.Interaction.RespondAsync("Please attach a valid image file.", ephemeral: true);
            return;
        }
        
        await ctx.Interaction.DeferAsync();
        try
        {
            using var baseImage = SixLabors.ImageSharp.Image.Load(await Global.HttpClient.GetByteArrayAsync(imageUri));
            bool gif = cntType.Equals("image/gif", StringComparison.OrdinalIgnoreCase);
            
            string bubblePath = Path.Combine("Assets", "speech-bubble.png");
            using var bubbleOverlay = await SixLabors.ImageSharp.Image.LoadAsync(bubblePath);
            
            // make width 100% and height 17.5% of the image being overlaid onto.
            int bubbleWidth = baseImage.Width;
            int bubbleHeight = (int)(baseImage.Height * 0.175);
            
            // don't let the height be too little or too much.
            if (bubbleHeight < bubbleWidth / 10) bubbleHeight = bubbleWidth / 10;
            if (bubbleHeight > baseImage.Height / 4) bubbleHeight = baseImage.Height / 4;
            bubbleOverlay.Mutate(x => x.Resize(bubbleWidth, bubbleHeight));

            // vertical pos, depends on if 'down' is set.
            int yPosition = 0;

            if (left) bubbleOverlay.Mutate(x => x.Flip(FlipMode.Horizontal));
            if (down)
            {
                bubbleOverlay.Mutate(x => x.Flip(FlipMode.Vertical));
                yPosition = baseImage.Height - bubbleHeight;
            }
            
            // if it's a gif then we gotta do all the frames.
            if (gif && baseImage.Frames.Count > 1)
            {
                for (int i = 0; i < baseImage.Frames.Count; i++)
                {
                    // convert imageframe to image so we can use Mutate
                    using var frameImage = baseImage.Frames.CloneFrame(i);
                    frameImage.Mutate(x => x.DrawImage(bubbleOverlay, new Point(0, yPosition), 1f));
                    // replace the frame at the baseImage
                    baseImage.Frames.RemoveFrame(i);
                    baseImage.Frames.InsertFrame(i, frameImage.Frames.RootFrame);
                }

                using var outputStream = new MemoryStream();
                await baseImage.SaveAsync(outputStream, new GifEncoder());
                outputStream.Position = 0;

                // TODO: in many cases this looks like SHIT
                // must find a way to flatten good keyframe frames, problem for the future, but must be done.
                await ctx.Interaction.FollowupWithFileAsync(
                    fileStream: outputStream,
                    fileName: $"{imageName.Split('.').First()}-bubble.gif"
                );
            }
            else // otherwise just overlay the one image and send as png
            {
                baseImage.Mutate(x => x.DrawImage(bubbleOverlay, new Point(0, yPosition), 1f));
                
                using var outputStream = new MemoryStream();
                await baseImage.SaveAsync(outputStream, new PngEncoder());
                outputStream.Position = 0;
                
                await ctx.Interaction.FollowupWithFileAsync(
                    fileStream: outputStream,
                    fileName: $"{imageName.Split('.').First()}-bubble.png"
                );
            }
        }
        catch (Exception ex)
        {
            await ctx.Interaction.FollowupAsync($"An error occurred while processing the image. {ex.Message}", ephemeral: true);
        }
    }
}
