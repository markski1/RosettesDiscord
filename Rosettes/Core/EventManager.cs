using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Rosettes.Modules.Commands.Alarms;
using Rosettes.Modules.Engine;
using Victoria;

namespace Rosettes.Core
{
    public static class EventManager
    {
        private static readonly LavaNode _lavaNode = ServiceManager.Provider.GetRequiredService<LavaNode>();
        private static readonly DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        private static readonly CommandService _commandService = ServiceManager.GetService<CommandService>();
        private static bool booting = true;

        public static Task LoadCommands()
        {
            if (booting)
            {
                booting = false;
            }
            else
            {
                return Task.CompletedTask;
            }

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

            _client.Disconnected += OnDisconnect;

            _client.JoinedGuild += OnJoinGuild;

            _client.LeftGuild += OnLeftGuild;

            return Task.CompletedTask;
        }

        // fired when booting
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


            if (Settings.ConnectToDatabase())
            {
                await UserEngine.LoadAllUsersFromDatabase();
                AlarmManager.LoadAllAlarmsFromDatabase();
            }
            else
            {
                Global.GenerateErrorMessage("OnReady", "Failed to connect to database.");
            }

            CommandEngine.CreateCommandPage();
            Game game = new("$commands", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
            await _client.SetActivityAsync(game);
            Console.WriteLine($"{DateTime.Now} | [READY] - Rosettes is nyow loaded.");
            await _client.SetStatusAsync(UserStatus.Online);
            _client.MessageReceived += OnMessageReceived;
        }

        // fired when a message is received
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

        /*
         * 
         * The callbacks below are used for analytics.
         * 
         */
        private static Task OnDisconnect(Exception ex)
        {
            Global.GenerateErrorMessage("OnReady", $"Rosettes has lost connection to Discord. {ex.Message}");

            return Task.CompletedTask;
        }

        private static Task OnJoinGuild(SocketGuild guild)
        {
            Global.GenerateNotification($"Rosettes has joined a new guild. **{guild.Name}**:*{guild.Id}* - {guild.MemberCount} members.");

            return Task.CompletedTask;
        }

        private static Task OnLeftGuild(SocketGuild guild)
        {
            Global.GenerateNotification($"Rosettes has left a guild. **{guild.Name}**:*{guild.Id}* - {guild.MemberCount} members.");

            return Task.CompletedTask;
        }
    }
}
