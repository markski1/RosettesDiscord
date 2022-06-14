using Discord.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rosettes.core;

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
    }
}