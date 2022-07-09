using Dapper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;

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

            bool result;

            try
            {
                result = await db.ExecuteScalarAsync<bool>(sql, new { Context.User.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("keygen-getcode", $"sqlException code {ex.Message}");
                return;
            }
            

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

        [Command("newvote")]
        [Summary("Creates a quick, simple vote with thumbs up and down. \nUsage: $newvote <question>")]
        public async Task NewVote(params string[] questionWords)
        {
            if (Context.Guild is null)
            {
                await ReplyAsync("You may only create polls within a guild.");
                return;
            }

            string question = "";

            foreach (string word in questionWords)
            {
                question += $"{word} ";
            }

            string displayName;
            SocketGuildUser? GuildUser = Context.User as SocketGuildUser;
            if (GuildUser is not null && GuildUser.Nickname.Length < 1)
            {
                displayName = GuildUser.Nickname;
            }
            else
            {
                displayName = Context.User.Username;
            }

            string message = $"[{displayName}] created a vote:```{question}```";

            var mid = await ReplyAsync(message);

            var emojiList = new List<Emoji>
            {
                new Emoji("👍"),
                new Emoji("👎")
            };

            await mid.AddReactionsAsync(emojiList);

            await Context.Message.DeleteAsync();
        }

        [Command("setautoroles")]
        [Summary("Creates the AutoRoles channel in the channel where it's used. AutoRoles must first be set up from the web panel.")]
        public async Task SetAutoRoles()
        {
            if (Context.Guild is null)
            {
                await ReplyAsync("This command must run in a guild.");
                return;
            }
            if (!Global.CheckSnep(Context.User.Id) && Context.User != Context.Guild.Owner)
            {
                await ReplyAsync("This command may only be used by the server owner.");
                return;
            }

            await AutoRolesEngine.SyncWithDatabase();

            var roles = AutoRolesEngine.GetGuildAutoroles(Context.Guild.Id);

            if (roles is null || !roles.Any())
            {
                await ReplyAsync("It looks like you haven't set up your AutoRoles yet. AutoRoles are set up through the web interface. Head over to https://markski.ar/rosettes -> Roles -> Set up Autoroles");
                return;
            }

            List<Emoji> emojis = new();

            string text = "";

            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            var restGuild = await dbGuild.GetDiscordRestReference();
            var socketGuild = dbGuild.GetDiscordSocketReference();

            var embed = new EmbedBuilder();

            embed.WithTitle("Autoroles!");
            embed.WithDescription("Click a reaction to get it's role.");

            foreach (var role in roles)
            {
                emojis.Add(new Emoji(role.Emote));
                string roleName = "";
                if (socketGuild is not null)
                {
                    roleName = socketGuild.GetRole(role.RoleId).Name;
                } else if (restGuild is not null)
                {
                    roleName = restGuild.GetRole(role.RoleId).Name;
                }
                text += $"{role.Emote} - {roleName}\n\n";
            }

            embed.AddField("Available roles: ", text);

            var mid = await ReplyAsync(embed: embed.Build());

            dbGuild.AutoRolesMessage = mid.Id;

            await mid.AddReactionsAsync(emojis);

            await GuildEngine.UpdateGuild(dbGuild);
        }
    }
}