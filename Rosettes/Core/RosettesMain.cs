using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Managers;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Guild;
using Rosettes.Modules.Engine.Minigame;

namespace Rosettes.Core;

public class RosettesMain
{
    private readonly DiscordSocketClient _client;
    private readonly System.Timers.Timer _syncTimer = new();

    public RosettesMain()
    {
        InteractionServiceConfig interactionConfig = new()
        {
            DefaultRunMode = RunMode.Async,
            LogLevel = Settings.LogSeverity
        };
        DiscordSocketConfig clientConfig = new()
        {
            AlwaysDownloadUsers = true,
            LargeThreshold = 250,
            MaxWaitBetweenGuildAvailablesBeforeReady = 10000,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent,
            LogLevel = Settings.LogSeverity
        };
        _client = new DiscordSocketClient(clientConfig);
        InteractionService commands = new(_client, interactionConfig);


        ServiceCollection collection = new();

        collection.AddSingleton(_client);
        collection.AddSingleton(commands);
        collection.AddSingleton<InteractionManager>();

        ServiceManager.SetProvider(collection);
    }

    public async Task MainAsync()
    {
        // Identify with the token and connect to discord.
        await _client.LoginAsync(TokenType.Bot, Settings.Token);
        await _client.StartAsync();

        // start thy stuff
        await EventManager.SetupAsync();

        // TenMinutyTimer(); defined below, runs every 10 minutes, or 600 seconds
        _syncTimer.Elapsed += SyncThings;
        _syncTimer.Interval = 600000;
        _syncTimer.AutoReset = true;
        _syncTimer.Enabled = true;

        await Task.Delay(-1);
    }

    private static void SyncThings(object? source, System.Timers.ElapsedEventArgs e)
    {
        Thread timedThread = new(TimedActions);
        timedThread.Start();
    }

    private static void TimedActions()
    {
        UserEngine.SyncWithDatabase();
        GuildEngine.SyncWithDatabase();
        PetEngine.TimedThings();
        PetEngine.SyncWithDatabase();
    }

    public static async Task<bool> HaltOrRestart(bool restart = false)
    {
        UserEngine.SyncWithDatabase();
        PetEngine.SyncWithDatabase();
        GuildEngine.SyncWithDatabase();
        Game game = new("Restarting, please wait!", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
        var client = ServiceManager.GetService<DiscordSocketClient>();
        await client.SetActivityAsync(game);

        bool success = true;

        if (restart)
        {
            try
            {
                // In the machine where Rosettes runs, the startRosettes.sh script located one directory above
                // properly initializes certain files and starts Rosettes as a background process through nohup <whatever> &
                // I find this more convenient than running it as a systemd service for reasons I don't care to discuss here.
                int runSuccess = await "../startRosettes.sh".RunBash();
                if (runSuccess != 0)
                {
                    success = false;
                }
            }
            catch
            {
                success = false;
            }
        }

        Environment.Exit(0);
        return success;
    }
}
