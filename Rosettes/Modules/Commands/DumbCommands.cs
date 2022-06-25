using Discord.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rosettes.Core;
using System.Text.RegularExpressions;

namespace Rosettes.Modules.Commands
{
    [Summary("Commands which are dumb.")]
    public class DumbCommands : ModuleBase<SocketCommandContext>
    {
        [Command("fakecat")]
        [Summary("Returns an AI generated picture of a cat.")]
        public async Task FakeCat()
        {
            if (Context.Guild is null)
            {
                await ReplyAsync("This command cannot be used in DM's. Instead use https://thiscatdoesnotexist.com/");
                return;
            }
            try
            {
                Stream data = await Global.HttpClient.GetStreamAsync($"https://thiscatdoesnotexist.com/");

                if (!Directory.Exists("./temp/")) Directory.CreateDirectory("./temp/");
                if (!Directory.Exists("./temp/pics/")) Directory.CreateDirectory("./temp/cats/");
                string fileName = $"./temp/pics/{Global.Random.Next(20) + 1}.jpg";

                if (File.Exists(fileName)) File.Delete(fileName);

                using var fileStream = new FileStream(fileName, FileMode.Create);
                await data.CopyToAsync(fileStream);
                fileStream.Close();

                await Context.Channel.SendFileAsync(fileName);
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("fakecat", $"{ex.Message}");
                await ReplyAsync($"Failed to fetch fake cat.");
            }
        }

        [Command("fakeperson")]
        [Summary("Returns an AI generated picture of an arguably human being.")]
        public async Task FakePerson()
        {
            if (Context.Guild is null)
            {
                await ReplyAsync("This command cannot be used in DM's. Instead use https://thispersondoesnotexist.com/");
                return;
            }
            try
            {
                Stream data = await Global.HttpClient.GetStreamAsync($"https://thispersondoesnotexist.com/image");

                if (!Directory.Exists("./temp/")) Directory.CreateDirectory("./temp/");
                if (!Directory.Exists("./temp/pics/")) Directory.CreateDirectory("./temp/peeps/");
                string fileName = $"./temp/pics/{Global.Random.Next(20) + 1}.jpg";

                if (File.Exists(fileName)) File.Delete(fileName);

                using var fileStream = new FileStream(fileName, FileMode.Create);
                await data.CopyToAsync(fileStream);
                fileStream.Close();

                await Context.Channel.SendFileAsync(fileName);
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("fakeperson", $"{ex.Message}");
                await ReplyAsync($"Failed to fetch fake person.");
            }
        }

        [Command("urban")]
        [Summary("Returns an UrbanDictionary definition for the provided word.")]
        public async Task UrbanDefine(string givenTerm = "")
        {
            if (givenTerm == "")
            {
                await ReplyAsync($"Usage: `{Settings.Prefix}urban <term>`");
                return;
            }
            if (!Regex.IsMatch(givenTerm, "^[a-zA-Z0-9]*$"))
            {
                await ReplyAsync($"The term must only contain letters and numbers.");
                return;
            }
            
            string message;

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://mashape-community-urban-dictionary.p.rapidapi.com/define?term={givenTerm.ToLower()}"),
                Headers =
                    {
                        { "X-RapidAPI-Key", Settings.RapidAPIKey },
                        { "X-RapidAPI-Host", "mashape-community-urban-dictionary.p.rapidapi.com" },
                    },
            };

            try
            {
                using var response = await Global.HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsStringAsync();
                // obtain the definition from the API and parse it. Use "first" because it's all contained in a response object.
                var definitionList = JObject.Parse(data).First;
                if (definitionList is null)
                {
                    await ReplyAsync("Failed to fetch definitions.");
                    return;
                }

                // "first" again because the definitions are inside a list (and as stated above, the list inside a response object. very stupid).
                definitionList = definitionList.First;

                // if the list is null, we have no results.
                if (definitionList is null)
                {
                    await ReplyAsync("No definition found for that word.");
                    return;
                }

                List<dynamic> parsedDefinitionList = new();
                foreach (var aDefinition in definitionList)
                {
                    dynamic? temp = JsonConvert.DeserializeObject(aDefinition.ToString());
                    if (temp is null) continue;
                    parsedDefinitionList.Add(temp);
                }

                dynamic definition = parsedDefinitionList.OrderBy(def => def.thumbs_up - def.thumbs_down).Last();

                message =
                    $"Definition for: {givenTerm}" +
                    $"```" +
                    definition.definition +
                    $"```" +
                    $"**Upvotes**: {definition.thumbs_up} | **Downvotes**: {definition.thumbs_down}\n" +
                    $"**Permalink**: <{definition.permalink}>";
            }
            catch (Exception ex)
            {
                message = "There was an error fetching the definition.";
                Global.GenerateErrorMessage("urbanDictionary", ex.Message);
            }

            await ReplyAsync(message);
        }
    }
}