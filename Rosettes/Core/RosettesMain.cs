using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Rosettes.Modules.Engine;
using Microsoft.Extensions.DependencyInjection;
using Victoria;

namespace Rosettes.Core
{
    public class RosettesMain
    {
        private readonly DiscordSocketClient Client;
        private readonly CommandService Commands;
        private readonly System.Timers.Timer HalfHourlyTimer = new();

        public RosettesMain()
        {
            CommandServiceConfig command_config = new()
            {
                DefaultRunMode = RunMode.Async,
                IgnoreExtraArgs = true,
                LogLevel = Settings.LogSeverity
            };
            DiscordSocketConfig client_config = new()
            {
                LogLevel = Settings.LogSeverity
            };
            Client = new(client_config);
            Commands = new(command_config);

            ServiceCollection collection = new();

            collection.AddSingleton(Client);
            collection.AddSingleton(Commands);
            if (Settings.LavaLinkData is not null)
            {
                collection.AddLavaNode(x =>
                {
                    x.SelfDeaf = true;
                    x.Hostname = Settings.LavaLinkData.Host;
                    x.Port = Settings.LavaLinkData.Port;
                    x.Authorization = Settings.LavaLinkData.Password;
                });
            }

            ServiceManager.SetProvider(collection);
        }

        public async Task MainAsync()
        {
            // Identify with the token and connect to discord.
            await Client.LoginAsync(TokenType.Bot, Settings.Token);
            await Client.StartAsync();

            // start thy stuff
            await CommandEngine.LoadCommands();
            await EventManager.LoadCommands();

            // HalfHourlyThings(); defined below, runs every 30 minutes, or 1800 seconds
            HalfHourlyTimer.Elapsed += HalfHourlyThings;
            HalfHourlyTimer.Interval = 1800000;
            HalfHourlyTimer.AutoReset = true;
            HalfHourlyTimer.Enabled = true;

            // Done! Now keep this task blocked forever to avoid the bot from closing.
            await Task.Delay(-1);
        }

        public void HalfHourlyThings(object? source, System.Timers.ElapsedEventArgs e)
        {
            UserEngine.SyncWithDatabase();
            GuildEngine.SyncWithDatabase();
            CommandEngine.SyncWithDatabase();
        }
    }
}
