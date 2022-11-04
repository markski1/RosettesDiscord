using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Rosettes.Modules.Engine;
using Microsoft.Extensions.DependencyInjection;
using Victoria;
using Discord.Rest;

namespace Rosettes.Core
{
    public class RosettesMain
    {
        private readonly DiscordSocketClient Client;
        private readonly CommandService Commands;
        private readonly System.Timers.Timer FiveMinutyTimer = new();

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
                AlwaysDownloadUsers = true,
                LargeThreshold = 250,
                MaxWaitBetweenGuildAvailablesBeforeReady = 10000,
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent,
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

            // TenMinutyThings(); defined below, runs every 10 minutes, or 600 seconds
            FiveMinutyTimer.Elapsed += FiveMinutyThings;
            FiveMinutyTimer.Interval = 300000;
            FiveMinutyTimer.AutoReset = true;
            FiveMinutyTimer.Enabled = true;

            // Done! Now keep this task blocked forever to avoid the bot from closing.
            await Task.Delay(-1);
        }

        public void FiveMinutyThings(object? source, System.Timers.ElapsedEventArgs e)
        {
            UserEngine.SyncWithDatabase();
            GuildEngine.SyncWithDatabase();
            CommandEngine.SyncWithDatabase();
        }
    }        
}
