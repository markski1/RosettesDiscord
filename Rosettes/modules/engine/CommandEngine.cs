using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Rosettes.core;
using Rosettes.modules.commands;

namespace Rosettes.modules.engine
{
    public static class CommandEngine
    {
        private static readonly CommandService _commands = ServiceManager.GetService<CommandService>();

        public static async Task LoadCommands()
        {
            // Load all the commands from their modules
            await _commands.AddModuleAsync<RandomCommands>(null);
            await _commands.AddModuleAsync<UtilityCommands>(null);
            await _commands.AddModuleAsync<MusicCommands>(null);
            await _commands.AddModuleAsync<GameCommands>(null);
            await _commands.AddModuleAsync<UnlistedCommands>(null);
        }

        public static async Task HandleCommand(SocketCommandContext context, int argPos)
        {
            var user = await UserEngine.GetDBUser(context.User);
            if (user.CanUseCommand())
            {
                await _commands.ExecuteAsync(context: context, argPos: argPos, services: ServiceManager.Provider);
            }
            else
            {
                await context.Message.AddReactionAsync(new Emoji("⌚"));
            }
        }

        public static void CreateCommandPage()
        {
            if (!Directory.Exists("/var/www/html/rosettes/"))
            {
                return;
            }

            string webContents = "";

            ModuleInfo? currModule = null;
            var comms = ServiceManager.GetService<CommandService>();
            foreach (CommandInfo singleCommand in comms.Commands)
            {
                // Every module has it's own list message to avoid surpasing the 2000char limit.
                if (singleCommand.Module.Name == "UnlistedCommands") break;
                if (currModule == null || currModule.Name != singleCommand.Module.Name)
                {
                    currModule = singleCommand.Module;
                    webContents += $"\n<h3>{currModule.Remarks}</h3> <p><b>{currModule.Summary}</b></p>";
                }
                webContents += $"<p><b>{Settings.Prefix}{singleCommand.Name}</b><br>";
                if (singleCommand.Summary != null)
                {
                    webContents += $"{singleCommand.Summary}</p>\n";
                }
                else
                {
                    webContents += $"\n\n";
                }
            }
            using var writer = File.CreateText("/var/www/html/rosettes/commands.html");

            writer.Write(webContents);

            writer.Close();
        }
    }
}
