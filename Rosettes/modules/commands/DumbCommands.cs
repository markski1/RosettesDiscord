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
                if (!Directory.Exists("./temp/cats/")) Directory.CreateDirectory("./temp/cats/");
                string fileName = $"./temp/cats/{Global.Random.Next(20) + 1}.jpg";

                if (File.Exists(fileName)) File.Delete(fileName);

                using var fileStream = new FileStream(fileName, FileMode.Create);
                await data.CopyToAsync(fileStream);
                fileStream.Close();

                await Context.Channel.SendFileAsync(fileName);
            }
            catch
            {
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
                if (!Directory.Exists("./temp/peeps/")) Directory.CreateDirectory("./temp/peeps/");
                string fileName = $"./temp/peeps/{Global.Random.Next(20) + 1}.jpg";

                if (File.Exists(fileName)) File.Delete(fileName);

                using var fileStream = new FileStream(fileName, FileMode.Create);
                await data.CopyToAsync(fileStream);
                fileStream.Close();

                await Context.Channel.SendFileAsync(fileName);
            }
            catch
            {
                await ReplyAsync($"Failed to fetch fake cat.");
            }
        }
    }
}