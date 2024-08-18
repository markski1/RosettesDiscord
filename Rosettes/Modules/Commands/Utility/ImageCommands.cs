using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Text.Encodings.Web;

namespace Rosettes.Modules.Commands.Utility;

[Group("image", "Image manipulation commands")]
public class ImageCommands : InteractionModuleBase<SocketInteractionContext>
{
    [MessageCommand("SauceNAO Search")]
    public async Task SauceNaoCtx(IMessage message)
    {
        string getUri = Global.GrabUriFromText(message.Content);

        // first try to find any image attached
        if (message.Attachments.Any())
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
        string getUrl = $"https://saucenao.com/search.php?output_type=2&api_key={Settings.SauceNAO}&url={url}";

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

        var dbUser = await UserEngine.GetDBUser(Context.User);

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
}