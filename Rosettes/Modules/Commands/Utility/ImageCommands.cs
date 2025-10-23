using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Text.Encodings.Web;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using Point = SixLabors.ImageSharp.Point;

namespace Rosettes.Modules.Commands.Utility;

[Group("image", "Image manipulation commands")]
public class ImageCommands : InteractionModuleBase<SocketInteractionContext>
{
    [MessageCommand("SauceNAO Search")]
    public async Task SauceNaoCtx(IMessage message)
    {
        string getUri = Global.GrabUriFromText(message.Content);

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
        if (getUri == "0")
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
        string getUri = Global.GrabUriFromText(message.Content);
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

        if (getUri == "0")
        {
            await RespondAsync("No images found in this message.", ephemeral: true);
            return;
        }
       
        await BubbleImage.CreateBubbleImage(Context, getUri, new Uri(getUri).LocalPath, cntType, false, false);
    }
    
    [SlashCommand("bubble", "Add a bubble overlay on an image.")]
    public async Task CmdBubble(
        [Summary("image", "Attached image to be used.")] IAttachment image,
        [Summary("down", "Wether the bubble should aim down")] bool down = false,
        [Summary("left", "Wether the bubble should aim left")] bool left = false
    )
    {
        if (image.ContentType == null || !image.ContentType.StartsWith("image/"))
        {
            await RespondAsync("Please attach a valid image file.", ephemeral: true);
            return;
        }
        
        await BubbleImage.CreateBubbleImage(Context, image.Url, image.Filename, image.ContentType, down, left);
    }
}

public static class BubbleImage
{
    public static async Task CreateBubbleImage(SocketInteractionContext ctx, string imageUri, string imageName, string cntType, bool down, bool left)
    {
        await ctx.Interaction.DeferAsync();
        try
        {
            var httpClient = new HttpClient();
            var baseImage = SixLabors.ImageSharp.Image.Load(await httpClient.GetByteArrayAsync(imageUri));
            bool gif = cntType.Equals("image/gif", StringComparison.OrdinalIgnoreCase);
            
            string bubblePath = Path.Combine("Assets", "speech-bubble.png");
            var bubbleOverlay = await SixLabors.ImageSharp.Image.LoadAsync(bubblePath);
            
            // make width 100% and height 15% of the image being overlaid onto.
            int bubbleWidth = baseImage.Width;
            int bubbleHeight = (int)(baseImage.Height * 0.15);
            
            // don't let the height be too little or too much.
            if (bubbleHeight < bubbleWidth / 10) bubbleHeight = bubbleWidth / 10;
            if (bubbleHeight > baseImage.Height / 5) bubbleHeight = baseImage.Height / 5;
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
        catch
        {
            await ctx.Interaction.FollowupAsync($"An error occurred while processing the image.", ephemeral: true);
        }
    }
}