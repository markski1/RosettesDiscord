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
        ServiceCollection collection = [];

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
        TelemetryEngine.Setup();

        // TenMinutyTimer(); defined below, runs every 20 minutes, or 1200 seconds
        _syncTimer.Elapsed += SyncThings;
        _syncTimer.Interval = 1200000;
        _syncTimer.AutoReset = true;
        _syncTimer.Enabled = true;

        Global.HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0");

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
}
