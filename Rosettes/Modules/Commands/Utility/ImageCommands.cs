using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Newtonsoft.Json;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands.Utility
{
    [Group("image", "Image manipulation commands")]
    public class ImageCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [MessageCommand("SauceNAO Search")]
        public async Task SauceNAOCtx(IMessage message)
        {
            string getUrl = Global.GrabURLFromText(message.Content);

            // first try to find any image attached
            if (message.Attachments.Any())
            {
                string fileType = message.Attachments.First().ContentType.ToLower();
                if (fileType.Contains("image/"))
                {
                    getUrl = message.Attachments.First().Url;
                }
            }

            // if still no luck, try to grab an emote.
            if (getUrl == "0")
            {
                try
                {
                    Emote emote = Emote.Parse(message.Content);
                    getUrl = emote.Url;
                }
                catch
                {
                    await RespondAsync("No images or emotes found in this message.", ephemeral: true);
                    return;
                }
            }

            await SauceNAO(getUrl);
        }

        [SlashCommand("saucenao", "Use SauceNAO to try and find the source of a provided image url.")]
        public async Task SauceNAO(string url)
        {
            string getUrl = $"https://saucenao.com/search.php?db=999&output_type=2&numres=1&dbmaski=25986063&api_key={Settings.SauceNAO}&url={url}";

            var response = await Global.HttpClient.GetStringAsync(getUrl);

            if (response is null)
            {
                await RespondAsync("Sorry, there was an error reaching the SauceNAO API. [SE1]", ephemeral: true);
                return;
            }

            var deserializedResponse = JsonConvert.DeserializeObject(response);

            if (deserializedResponse is null)
            {
                await RespondAsync("Sorry, there was an error reaching the SauceNAO API. [SE2]", ephemeral: true);
                return;
            }

            dynamic responseObj = deserializedResponse as dynamic;

            var dbUser = await UserEngine.GetDBUser(Context.User);

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = "SauceNAO Top Result";

            bool found = false;



            if (responseObj.header.long_remaining < 1 || responseObj.header.long_remaining < 1)
            {
                embed.Description = "We are currently rate-limited by SauceNAO. This usually fixes itself after a minute or two, so try again in a bit, or see the results directly on SauceNAO.";
            }
            else
            {
                foreach (var item in responseObj.results)
                {
                    found = true;
                    embed.ThumbnailUrl = item.header.thumbnail;
                    embed.AddField("Similarity", $"{item.header.similarity} percent");
                    string sources = "";
                    foreach (var src in item.data.ext_urls)
                    {
                        sources += $"{src}\n";
                    }
                    embed.AddField("Source URLs", sources);
                }

                if (!found)
                {
                    await RespondAsync("No results.", ephemeral: true);
                    return;
                }
            }

            ComponentBuilder comps = new();

            comps.WithButton("See results on SauceNAO", style: ButtonStyle.Link, url: $"https://saucenao.com/search.php?url={url}");

            try
            {
                await RespondAsync(embed: embed.Build(), components: comps.Build());
            }
            // if we took too long to respond we'll have an exception, then do it as a reply.
            catch
            {
                try
                {
                    await ReplyAsync(embed: embed.Build(), components: comps.Build());
                }
                // if we don't have permissions, just fail.
                catch
                {
                    // don't crash.
                }
            }
        }






        [MessageCommand("Convert Image")]
        public async Task ConvertImageCtx(IMessage message)
        {
            string getUrl = Global.GrabURLFromText(message.Content);

            // first try to find any image attached
            if (message.Attachments.Any())
            {
                string fileType = message.Attachments.First().ContentType.ToLower();
                if (fileType.Contains("image/"))
                {
                    getUrl = message.Attachments.First().Url;
                }
            }

            // if still no luck, try to grab an emote.
            if (getUrl == "0")
            {
                try
                {
                    Emote emote = Emote.Parse(message.Content);
                    getUrl = emote.Url;
                }
                catch
                {
                    await RespondAsync("No images or emotes found in this message.", ephemeral: true);
                    return;
                }
            }

            await ConvertImage(getUrl);
        }

        [SlashCommand("convert", "Convert an image to a desired format.")]
        public async Task ConvertImage(string getUrl)
        {
            string randomValue = $"{Global.Randomize(90) + 10}";
            if (!Directory.Exists("/var/www/html/brickthrow/convertQueue/"))
            {
                Directory.CreateDirectory("/var/www/html/brickthrow/convertQueue/");
            }
            if (!Directory.Exists("/var/www/html/brickthrow/generated/"))
            {
                Directory.CreateDirectory("/var/www/html/brickthrow/generated/");
            }
            string fileName = $"/var/www/html/brickthrow/convertQueue/{randomValue}.image";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            using (Stream stream = await Global.HttpClient.GetStreamAsync(getUrl))
            {
                using var fileStream = new FileStream(fileName, FileMode.Create);
                var downloadTask = stream.CopyToAsync(fileStream);
                int quarterSecondCount = 0;
                while (!downloadTask.IsCompleted)
                {
                    await Task.Delay(250);
                    quarterSecondCount++;
                    if (quarterSecondCount > 10) // if the download takes more than 2.5 seconds it's probably not a very honest url
                    {
                        await RespondAsync("Cancelled: Image download took too long.", ephemeral: true);
                        // Can't dipose an unfinished task, but upon testing, the GC consistently takes care of this
                        return;
                    }
                }
            }

            EmbedBuilder embed = await Global.MakeRosettesEmbed();

            embed.Title = "Image converter";
            embed.Description = "Choose what format you wanna convert to:";

            var buttons = new ActionRowBuilder();

            buttons.WithButton(label: "PNG", customId: $"CONVERT {randomValue} 1", style: ButtonStyle.Primary);
            buttons.WithButton(label: "JPG", customId: $"CONVERT {randomValue} 2", style: ButtonStyle.Primary);
            buttons.WithButton(label: "GIF", customId: $"CONVERT {randomValue} 3", style: ButtonStyle.Primary);
            buttons.WithButton(label: "WEBP", customId: $"CONVERT {randomValue} 4", style: ButtonStyle.Primary);
            buttons.WithButton(label: "BMP", customId: $"CONVERT {randomValue} 5", style: ButtonStyle.Primary);

            ComponentBuilder comps = new();

            comps.AddRow(buttons);

            await RespondAsync(embed: embed.Build(), components: comps.Build(), ephemeral: true);
        }








        [MessageCommand("Reverse GIF")]
        public async Task ReverseGIFMessageCMD(IMessage message)
        {
            string getUrl = Global.GrabURLFromText(message.Content);

            // first try to find a gif attached
            if (message.Attachments.Any())
            {
                string fileType = message.Attachments.First().ContentType.ToLower();
                if (fileType.Contains("/gif"))
                {
                    getUrl = message.Attachments.First().Url;
                }
            }
            // else, check if it's a tenor url. If that's the case, we need to get a direct link to the gif through the API.
            else if (getUrl.Contains("tenor.com"))
            {
                getUrl = await ImageHelper.GetDirectTenorURL(getUrl);
            }

            // if we got a url to fetch, go for it.
            if (getUrl != "0")
            {
                await ReverseGIF(getUrl);
            }
            else
            {
                // else, last attempt to get something: try to fetch it out of an emote.
                try
                {
                    Emote emote = Emote.Parse(message.Content);
                    await ReverseGIF(emote.Url);
                }
                catch
                {
                    // welp, we found nothing to work with.
                    await RespondAsync("No images or animated emotes found in this message.", ephemeral: true);
                }
            }
        }

        [SlashCommand("reverse-gif", "[experimental] Reverse the gif in the provided URL.")]
        public async Task ReverseGIFSlashCMD(string gifUrl)
        {
            string getUrl = Global.GrabURLFromText(gifUrl);

            // check if it's a tenor url. If that's the case, we need to get a direct link to the gif through the API.
            if (getUrl.Contains("tenor.com"))
            {
                getUrl = await ImageHelper.GetDirectTenorURL(getUrl);
            }

            if (getUrl != "0")
            {
                await ReverseGIF(getUrl);
            }
            else
            {
                await RespondAsync("Sorry, there was an error fetching the gif.", ephemeral: true);
            }
        }

        public async Task ReverseGIF(string url)
        {
            string randomValue = $"{Global.Randomize(100)}";
            if (!Directory.Exists("/var/www/html/brickthrow/reverseCache/"))
            {
                Directory.CreateDirectory("/var/www/html/brickthrow/reverseCache/");
            }
            if (!Directory.Exists("/var/www/html/brickthrow/generated/"))
            {
                Directory.CreateDirectory("/var/www/html/brickthrow/generated/");
            }
            string fileName = $"/var/www/html/brickthrow/reverseCache/{randomValue}.gif";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            await DeferAsync();

            using (Stream stream = await Global.HttpClient.GetStreamAsync(url))
            {
                using var fileStream = new FileStream(fileName, FileMode.Create);
                var downloadTask = stream.CopyToAsync(fileStream);
                int quarterSecondCount = 0;
                while (!downloadTask.IsCompleted)
                {
                    await Task.Delay(250);
                    quarterSecondCount++;
                    if (quarterSecondCount >= 12) // if the download takes more than 3 seconds it's probably not a very honest url
                    {
                        await FollowupAsync("Cancelled: GIF download took too long.");
                        // Can't dipose an unfinished task, but upon testing, the GC consistently takes care of this
                        return;
                    }
                }
            }

            fileName = $"/var/www/html/brickthrow/generated/{randomValue}.gif";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (var stream = await Global.HttpClient.GetStreamAsync($"https://snep.markski.ar/brickthrow/reverse.php?imageNum={randomValue}"))
            {
                using var fileStream = new FileStream(fileName, FileMode.Create);
                await stream.CopyToAsync(fileStream);
            }
            ulong size = (ulong)new FileInfo(fileName).Length;

            if (size > 1024)
            {
                await FollowupWithFileAsync(fileName);
            }
            else
            {
                await FollowupAsync("The provided file was not a gif.", ephemeral: true);
            }
            File.Delete(fileName);
        }


        [SlashCommand("throwbrick", "Generate a GIF of a provided emote throwing a brick.")]
        public async Task ThrowBrick([Summary("emote", "Provide an emote to use in the GIF.")] string emote = "none", [Summary("user", "Provide a user to use their avatar in the GIF.")] IGuildUser? user = null, [Summary("image-url", "Provide a URL to use in the GIF.")] string imageUrl = "none", [Summary("reverse", "Use \"true\" to reverse the GIF.")] string reverse = "false", string parry = "false")
        {
            Emote? emoteExtract;

            string brickerUrl;

            if (user != null)
            {
                try
                {
                    if (user.GetDisplayAvatarUrl() is not null)
                    {
                        brickerUrl = user.GetDisplayAvatarUrl();
                    }
                    else
                    {
                        brickerUrl = user.GetDefaultAvatarUrl();
                    }
                }
                catch
                {
                    await RespondAsync("No valid user provided.", ephemeral: true);
                    return;
                }
            }
            else if (imageUrl != "none")
            {
                brickerUrl = imageUrl;
            }
            else if (emote != "none")
            {
                try
                {
                    emoteExtract = Emote.Parse(emote);
                    brickerUrl = emoteExtract.Url;
                }
                catch
                {
                    await RespondAsync("No valid user provided.", ephemeral: true);
                    return;
                }
            }
            else
            {
                await RespondAsync("You must provide an Emote, an Image Url or a User.", ephemeral: true);
                return;
            }

            await DoBrickThrow(brickerUrl, reverse != "false", parry != "false");
        }

        public async Task DoBrickThrow(string url, bool reverse = false, bool parry = false)
        {
            string randomValue = $"{Global.Randomize(100)}";
            if (!Directory.Exists("/var/www/html/brickthrow/emojiCache/"))
            {
                Directory.CreateDirectory("/var/www/html/brickthrow/emojiCache/");
            }
            if (!Directory.Exists("/var/www/html/brickthrow/generated/"))
            {
                Directory.CreateDirectory("/var/www/html/brickthrow/generated/");
            }
            string fileName = $"/var/www/html/brickthrow/emojiCache/{randomValue}.png";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            await DeferAsync();
            using (var stream = await Global.HttpClient.GetStreamAsync(url))
            {
                using var fileStream = new FileStream(fileName, FileMode.Create);
                await stream.CopyToAsync(fileStream);
            }
            fileName = $"/var/www/html/brickthrow/generated/{randomValue}.gif";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            if (reverse)
            {
                randomValue += "&reverse";
            }
            if (parry)
            {
                randomValue += "&parry";
            }
            using (var stream = await Global.HttpClient.GetStreamAsync($"https://snep.markski.ar/brickthrow/brickthrow.php?emojiNum={randomValue}"))
            {
                using var fileStream = new FileStream(fileName, FileMode.Create);
                await stream.CopyToAsync(fileStream);
            }
            ulong size = (ulong)new FileInfo(fileName).Length;
            if (size > 10024)
            {
                await FollowupWithFileAsync(fileName);
            }
            else
            {
                await FollowupAsync("Sorry, I was unable to do that.", ephemeral: true);
            }
			File.Delete(fileName);
		}
    }

    public static class ImageHelper
    {
        public static async Task ContinueImageConversion(SocketMessageComponent component)
        {
            string action = component.Data.CustomId;

            char type = action[11];

            char imageLoc = action[8];
            char imageLoc2 = action[9];

            string format = type switch
            {
                '1' => "png",
                '2' => "jpg",
                '3' => "gif",
                '4' => "webp",
                '5' => "bmp",
                _ => "no"
            };

            if (format == "no")
            {
                await component.RespondAsync("There was an error trying to convert the image.", ephemeral: true);
                return;
            }

            var dbUser = await UserEngine.GetDBUser(component.User);

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Author.Name = $"Requested by {embed.Author.Name}";

            embed.Title = "Image conversion";
            embed.Description = $"Converting image to {format}\nIn progress...";

            await component.RespondAsync(embed: embed.Build());

            var fileName = $"/var/www/html/brickthrow/generated/{imageLoc}.{format}";

            using (var stream = await Global.HttpClient.GetStreamAsync($"https://snep.markski.ar/brickthrow/convert.php?fileName={imageLoc}{imageLoc2}&format={format}"))
            {
                using var fileStream = new FileStream(fileName, FileMode.Create);
                await stream.CopyToAsync(fileStream);
            }
            ulong size = (ulong)new FileInfo(fileName).Length;

            if (size > 1024)
            {
                await component.FollowupWithFileAsync(fileName);
                embed.Description = $"Image converted to {format}";
                await component.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
            }
            else
            {
                embed.Description = $"Converting image to {format}\nThere was an error.";
                await component.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
            }
			File.Delete(fileName);
		}


        public static async Task<string> GetDirectTenorURL(string tenorUrl)
        {
            // check it isn't a direct link already
            if ((tenorUrl.Contains("/media.tenor") || tenorUrl.Contains("/c.tenor")) && tenorUrl.Contains(".gif"))
            {
                return tenorUrl;
            }

            int ends = tenorUrl.Length;

            // a valid tenor url will end with a number.
            if (char.IsNumber(tenorUrl[ends - 1]))
            {
                // the number in question is the post ID, which we must extract out of the url
                // to do this, check where the number begins by working our way back until there's no more numbers.
                int start = ends - 1;
                try
                {
                    while (char.IsNumber(tenorUrl[start]))
                    {
                        start--;
                    }
                }
                catch
                {
                    return "0";
                }
                start++;
                // now that we know where the number begins and ends, extract it and use it to ask the API for the media url.
                string id = tenorUrl[start..ends];
                string requestUrl = $"https://tenor.googleapis.com/v2/posts?key={Settings.TenorKey}&ids={id}";
                var data = await Global.HttpClient.GetStringAsync(requestUrl);

                // deserialize it into a dynamic object.
                var DeserialziedObject = JsonConvert.DeserializeObject(data);
                if (DeserialziedObject == null)
                {
                    return "0";
                }
                dynamic results = ((dynamic)DeserialziedObject).results;

                try
                {
                    foreach (var result in results)
                    {
                        // try to return a 'mediumgif' element, we can't really check that it's in the dynamic object though
                        try
                        {
                            return result.media_formats.mediumgif.url;
                        }
                        // if there's no mediumgif we'll face an exception, in which case just return the base gif
                        catch
                        {
                            return result.media_formats.gif.url;
                        }
                    }
                }
                catch { return "0"; }
            }
            return "0";
        }
    }
}