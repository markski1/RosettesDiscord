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
    }
}
