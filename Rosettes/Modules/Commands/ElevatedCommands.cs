using Dapper;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Diagnostics;

namespace Rosettes.Modules.Commands
{
    public class ElevatedCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("memory", "[PRIVILEGED COMMAND]")]
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
            runtimeText += $"{elapsed.Minutes} minute{((elapsed.Minutes != 1) ? 's' : null)}.";
            // we divide the privatememorysize in half because it includes a bunch of runtime memory usage which isn't the bot's problem, and it seems to consistently be half, plus minus 5 or 10%
            string text =
                $"```I am using {(ulong)((proc.PrivateMemorySize64 / 1024) * 0.5):N0} Kb of memory, across {proc.Threads.Count} threads.\n" +
                $"I've been running for {runtimeText}\n" +
                $"\n" +
                $"THREAD LIST\n\n";
            bool separator = true;
            int id = 0;
            foreach (ProcessThread thread in proc.Threads)
            {
                string newLine = $"ID {id} // TIME {thread.TotalProcessorTime.Milliseconds:N0}MS";
                id++;
                text += newLine;
                if (separator)
                {
                    int spacing = 22 - newLine.Length;
                    for (int i = 0; i < spacing; i++)
                    {
                        text += " ";
                    }
                    text += "|   ";
                } else
                {
                    text += "\n";
                }
                separator = !separator;
            }
            text += "\n\nSnow leopards are organized and precise team workers.```";
            await RespondAsync(text);
        }

        [SlashCommand("halt", "[PRIVILEGED COMMAND]")]
        public async Task Halt()
        {
            if (!Global.CheckSnep(Context.User.Id))
            {
                await ReplyAsync("This command is snep exclusive.");
                return;
            }
            UserEngine.SyncWithDatabase();
            GuildEngine.SyncWithDatabase();

            await ReplyAsync("Disconnecting from Discord...");
            Game game = new("Disconnecting!", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
            var client = ServiceManager.GetService<DiscordSocketClient>();
            await client.SetActivityAsync(game);
            Environment.Exit(0);
        }

        [SlashCommand("keygen", "Generates a unique key for logging into the Rosettes admin panel.")]
        public async Task KeyGen()
        {
            if (Context.Guild is not null)
            {
                await RespondAsync("Keys are private! You can't generate a new key while in a guild, you must do it in a private message.");
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

            await RespondAsync($"New unique key generated. Anyone with this key can change Rosettes settings for your servers, so beware.\nIf you ever want to change your key, just use $KeyGen again.");
            await RespondAsync($"```{NewKey}```");
        }
    }

    public class MessageDeleter
    {
        private readonly System.Timers.Timer Timer = new();
        private readonly Discord.Rest.RestUserMessage message;

        public MessageDeleter(Discord.Rest.RestUserMessage _message, int seconds)
        {
            Timer.Elapsed += DeleteMessage;
            Timer.Interval = seconds * 1000;
            message = _message;
            Timer.Enabled = true;
        }

        public void DeleteMessage(Object? source, System.Timers.ElapsedEventArgs e)
        {
            message.DeleteAsync();
            Timer.Stop();
            Timer.Dispose();
        }
    }
}