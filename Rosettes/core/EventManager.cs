using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Rosettes.modules.engine;
using Victoria;

namespace Rosettes.core
{
    public static class EventManager
    {
        private static readonly LavaNode _lavaNode = ServiceManager.Provider.GetRequiredService<LavaNode>();
        private static readonly DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        private static readonly CommandService _commandService = ServiceManager.GetService<CommandService>();

        public static Task LoadCommands()
        {
            _client.Log += message =>
            {
                Console.WriteLine($"{DateTime.Now} | {message.ToString()}");
                return Task.CompletedTask;
            };
            _commandService.Log += message =>
            {
                Console.WriteLine($"{DateTime.Now} | {message.ToString()}");
                return Task.CompletedTask;
            };

            _client.Ready += OnReady;
            return Task.CompletedTask;
        }

        private static async Task OnReady()
        {
            try
            {
                await _lavaNode.ConnectAsync();
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("OnReady", $"Failed to connect lavanode. {ex.Message}");
            }
            Global.GenerateErrorMessage("test", "amongus");
            CommandEngine.CreateCommandPage();
            Game game = new("$commands", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
            await _client.SetActivityAsync(game);
            Console.WriteLine($"{DateTime.Now} | [READY] - Rosettes is nyow loaded.");
            await _client.SetStatusAsync(UserStatus.Online);
            _client.MessageReceived += OnMessageReceived;
        }

        private static async Task OnMessageReceived(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);

#if !DEBUG
            if (context.Channel.Id == 971467116165353485) return;
#endif

            int argPos = 0;

            // halt if it's not a valid user message.
            if (message == null || message.Author.IsBot)
            {
                return;
            }

            // if it's a valid user message but not a command, pass it over to the message handler.
            if (message.HasCharPrefix(Settings.Prefix, ref argPos))
            {
                await CommandEngine.HandleCommand(context, argPos);
            } else
            {
                await MessageEngine.HandleMessage(context);
            }
        }
    }
}
