using Dapper;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Diagnostics;

namespace Rosettes.Modules.Commands
{
    public class ElevatedCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("about", "About rosettes.")]
        public async Task GetMemory()
        {
            using Process proc = Process.GetCurrentProcess();
            TimeSpan elapsed = DateTime.Now - proc.StartTime;
            string runtimeText = "";
            if (elapsed.Days > 0)
            {
                runtimeText += $"{elapsed.Days} day{((elapsed.Days != 1) ? 's' : null)}, ";
            }
            if (elapsed.Hours > 0)
            {
                runtimeText += $"{elapsed.Hours} hour{((elapsed.Hours != 1) ? 's' : null)}, ";
            }
            runtimeText += $"{elapsed.Minutes} minute{((elapsed.Minutes != 1) ? 's' : null)} ago.";

            var client = ServiceManager.GetService<DiscordSocketClient>();

            EmbedBuilder embed = await Global.MakeRosettesEmbed();
            embed.Title = "About Rosettes.";
            embed.Description = "A simple, free, open source discord bot.";
            embed.ThumbnailUrl = "https://markski.ar/images/rosettes.png";

            embed.AddField("Memory in use", $"{(ulong)((proc.PrivateMemorySize64 / 1024) * 0.5):N0} Kb", inline: true);
            embed.AddField("Threads", $"{proc.Threads.Count}", inline: true);
            embed.AddField("Last updated", runtimeText);
            embed.AddField("Currently serving", $"{client.Guilds.Count} guilds.", inline: true);
            embed.AddField("Ping to Discord", $"{client.Latency}ms", inline: true);
            embed.AddField("Learn about me", "<https://markski.ar/rosettes>");

            EmbedFooterBuilder footer = new() { Text = "Developed and maintained by Markski. | https://markski.ar", IconUrl = "https://markski.ar/images/profileDesplacement.png" };

            embed.Footer = footer;

            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("devcmd", "Developer command.")]
        public async Task AdminMenu(string function)
        {
            if (!Global.CheckSnep(Context.User.Id))
            {
                await ReplyAsync("This command is snep exclusive.");
                return;
            }
            if (function is "halt" or "restart")
            {
                await ReplyAsync("Syncing cache data with database...");
                UserEngine.SyncWithDatabase();
                GuildEngine.SyncWithDatabase();
                Game game = new("Restarting, please wait!", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
                var client = ServiceManager.GetService<DiscordSocketClient>();
                await client.SetActivityAsync(game);

                if (function is "restart")
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "Rosettes is restarting...");
                }
                else
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "Rosettes is shutting down.");
                }

                int success = await Global.RunBash("../startRosettes.sh");

                if (success == 0)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "Rosettes succesfully restarted.");
                }

                Environment.Exit(0);
            }
        }

        [SlashCommand("keygen", "Generates a unique key for logging into the Rosettes admin panel.")]
        public async Task KeyGen()
        {
            if (Context.Guild is not null)
            {
                await RespondAsync("Keys are private! You can't generate a new key while in a guild, you must do it in a private message.", ephemeral: true);
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
                if (rand.Next(0, 2) == 0)
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
                await RespondAsync("Sorry, there was an error generating a logon key for you. Please try again in a while.");
                Global.GenerateErrorMessage("keygen", $"Error! {ex.Message}");
                return;
            }

            await RespondAsync($"New unique key generated. This key is to be used in the webpanel, at https://snep.markski.ar/rosettes\n\nAnyone with this key can change Rosettes' settings in guilds owned by you.\nIf you ever want to change your key, just use /keygen again.");
            await ReplyAsync($"```{NewKey}```");
        }
    }
}