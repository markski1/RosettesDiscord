using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Diagnostics;

namespace Rosettes.Modules.Commands
{
    public class ElevatedCommands : ModuleBase<SocketCommandContext>
    {
        [Command("memory")]
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
            await ReplyAsync(text);
        }

        [Command("halt")]
        public async Task Halt()
        {
            if (!Global.CheckSnep(Context.User.Id))
            {
                await ReplyAsync("This command is snep exclusive.");
                return;
            }
            UserEngine.SyncWithDatabase();
            GuildEngine.SyncWithDatabase();
            CommandEngine.SyncWithDatabase();

            await ReplyAsync("Disconnecting from Discord...");
            Game game = new("Disconnecting!", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
            var client = ServiceManager.GetService<DiscordSocketClient>();
            await client.SetActivityAsync(game);
            Environment.Exit(0);
        }

        [Command("commands")]
        public async Task ListCommands(string argument = "")
        {
            if (argument.ToLower() != "dm")
            {
                await ReplyAsync("A full list of commands is available at https://snep.markski.ar/rosettes/commands.html");
                await ReplyAsync($"Alternatively, use `{Settings.Prefix}commands dm` to have them sent to your DM's. This is less convenient.");
                return;
            }

            var guild = Context.Guild;
            if (guild != null)
            {
                await ReplyAsync("Sending commands in DM's.");
            }

            // create a dm channel with the user.
            IDMChannel userDM;
            userDM = await Context.Message.Author.CreateDMChannelAsync();

            ModuleInfo? currModule = null;
            string text = "";
            var comms = ServiceManager.GetService<CommandService>();
            foreach (CommandInfo singleCommand in comms.Commands)
            {
                if (singleCommand.Module.Name == "ElevatedCommands") break;
                if (currModule == null || currModule.Name != singleCommand.Module.Name)
                {
                    if (currModule != null)
                    {
                        text += "```";
                        // If we're not at 1000 chars yet, stay on the same message. Otherwise send.
                        if (text.Length > 1000)
                        {
                            await userDM.SendMessageAsync(text);
                            text = "";
                        }
                    }
                    currModule = singleCommand.Module;
                    text += $"```\n{currModule.Name}\n> {currModule.Summary}\n====================\n\n";
                }
                text += $"{Settings.Prefix}{singleCommand.Name}";
                if (singleCommand.Summary != null)
                {
                    text += $":\n{singleCommand.Summary}\n\n";
                }
                else
                {
                    text += $"\n\n";
                }
            }
            text += "```";
            await userDM.SendMessageAsync(text);
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