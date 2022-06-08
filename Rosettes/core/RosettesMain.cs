using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Rosettes.modules.engine;
using Microsoft.Extensions.DependencyInjection;
using Victoria;

namespace Rosettes.core
{
    public class RosettesMain
    {
        private readonly DiscordSocketClient Client;
        private readonly CommandService Commands;
#if !DEBUG
        private readonly System.Timers.Timer ConstantTimer = new(15000);
#endif

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
            collection.AddLavaNode(x =>
            {
                x.SelfDeaf = true;
                x.Hostname = Settings.LavaLinkData.Host;
                x.Port = Settings.LavaLinkData.Port;
                x.Authorization = Settings.LavaLinkData.Password;
            });

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
            UserEngine.LoadAllUsersFromDatabase();

            // call ConstantChecks every 15 seconds
#if !DEBUG
            ConstantTimer.Elapsed += ConstantChecks;
            ConstantTimer.AutoReset = true;
            ConstantTimer.Enabled = true;
#endif

            // Done! Now keep this task blocked forever to avoid the bot from closing.
            await Task.Delay(-1);
        }

        public async void ConstantChecks(object? source, System.Timers.ElapsedEventArgs e)
        {
            if (File.Exists("shutdown"))
            {
                File.Delete("shutdown");
                UserEngine.SyncWithDatabase();
                Game game = new("Disconnecting!", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
                var client = ServiceManager.GetService<DiscordSocketClient>();
                await client.SetActivityAsync(game);
                Environment.Exit(0);
            }
        }
    }
}
