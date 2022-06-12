using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Rosettes.core;
using Rosettes.modules.engine;
using System.Diagnostics;

namespace Rosettes.modules.commands
{
    public class UnlistedCommands : ModuleBase<SocketCommandContext>
    {
        [Command("mewmory")]
        public async Task GetMewmoryUsage()
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
            string text =
                $"```I am using {(ulong)((proc.PrivateMemorySize64 / 1024) * 0.5):N0} Kb of mewmory, across {proc.Threads.Count} threads.\n" +
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
        public async Task HaltAsync()
        {
            if (!Global.CheckSnep(Context.User.Id))
            {
                await ReplyAsync("This command is snep exclusive.");
                return;
            }
            UserEngine.SyncWithDatabase();

            await ReplyAsync("Disconnecting from Discord...");
            Game game = new("Disconnecting!", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
            var client = ServiceManager.GetService<DiscordSocketClient>();
            await client.SetActivityAsync(game);
            Environment.Exit(0);
        }

        [Command("commands")]
        [Summary("List of commands.")]
        public async Task ListCommands()
        {
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
                // Every module has it's own list message to avoid surpasing the 2000char limit.
                if (singleCommand.Module.Name == "UnlistedCommands") break;
                if (currModule == null || currModule.Name != singleCommand.Module.Name)
                {
                    if (currModule != null)
                    {
                        text += "```";
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
}