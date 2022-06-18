using Discord.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rosettes.core;
using System.Text.RegularExpressions;

namespace Rosettes.modules.commands
{
    [Summary("Commands which are dumb.")]
    public class DumbCommands : ModuleBase<SocketCommandContext>
    {
        [Command("fakecat")]
        [Summary("Returns an AI generated picture of a cat.")]
        public async Task FakeCatAsync()
        {
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
        public async Task FakePersonAsync()
        {
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
        public async Task UrbanDefineAsync(string givenTerm = "")
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
            dynamic? definition = null;

            try
            {
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

                // "first" again because the definitions are inside a list.
                definitionList = definitionList.First;

                // if the list is null, we have no results.
                if (definitionList is null)
                {
                    await ReplyAsync("No definition found for that word.");
                    return;
                }

                int bestScore = 0;
                // go through every definition and choose the one with the highest score, put it in 'definition'
                foreach (var aDefinition in definitionList)
                {
                    dynamic? temp = JsonConvert.DeserializeObject(aDefinition.ToString());
                    if (temp is null) continue;
                    if (temp.thumbs_up - temp.thumbs_down > bestScore)
                    {
                        bestScore = temp.thumbs_up - temp.thumbs_down;
                        definition = temp;
                    } 
                }

                // shouldn't be possible, but just in case and to make the compiler happy.
                if (definition == null)
                {
                    await ReplyAsync("Failed to obtain definition.");
                    return;
                }

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