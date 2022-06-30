using Dapper;
using Discord;
using Discord.Commands;
using MySqlConnector;
using Rosettes.Core;

namespace Rosettes.Modules.Commands
{
    [Summary("Commands for administrative actions.")]
    public class AdminCommands : ModuleBase<SocketCommandContext>
    {
        [Command("keygen")]
        [Summary("Generates a unique key for logging into https://snep.markski.ar/rosettes ; the Rosettes server admin panel.")]
        public async Task KeyGen()
        {
            if (Context.Guild is not null)
            {
                await ReplyAsync("Keys are private! You can't generate a new key while in a guild, you must do it in a private message.");
                return;
            }

            var db = new MySqlConnection(Settings.Database.ConnectionString);

            var sql = @"SELECT count(1) FROM login_keys WHERE id=@Id";

            bool result = await db.ExecuteScalarAsync<bool>(sql, new { Context.User.Id });

            if (result)
            {
                sql = @"UPDATE login_keys SET login_key=@NewKey WHERE id=@Id";
            }
            else
            {
                sql = @"INSERT INTO login_keys (id, login_key)
                        VALUES(@Id, @NewKey)";
            }

            Random rand = new();
            int randNumba;
            string NewKey = "";
            char Character;
            int offset;
            for (int i = 0; i < 64; i++)
            {
                if (rand.Next(0,2) == 0)
                {
                    offset = 65;
                }
                else
                {
                    offset = 97;
                }
                randNumba = rand.Next(0, 26);
                Character = Convert.ToChar(randNumba + offset);
                NewKey += Character;
            }

            try
            {
                await db.ExecuteAsync(sql, new { Context.User.Id, NewKey });
            }
            catch (Exception ex)
            {
                await ReplyAsync("Sorry, there was an error generating a logon key for you. Please try again in a while.");
                Global.GenerateErrorMessage("keygen", $"Error! {ex.Message}");
                return;
            }

            await ReplyAsync($"New unique key generated. Anyone with this key can change Rosettes settings for your servers, so beware.\nIf you ever want to change your key, just use $KeyGen again.");
            await ReplyAsync($"```{NewKey}```");
        }
    }
}