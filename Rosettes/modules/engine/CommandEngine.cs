/*  
 *	CommandEngine
 *
 *	The CommandEngine class contains the base methods required for Rosettes to
 *	pick up the commands declared in other classes and add them to the command engine.
 *	Furthermore, it also handles invoking the execution of commands.
 *
 *		- Hooks the "HandleCommandAsync" method to the message handler.
 *		- Loads the commands from the rest of the bot's code.
 *		- Evaluates and runs the commands.
 */
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
