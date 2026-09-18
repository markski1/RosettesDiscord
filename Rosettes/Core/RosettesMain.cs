using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Managers;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Guild;
using Rosettes.Modules.Minigame.Pets;

namespace Rosettes.Core;

public class RosettesMain
{
    private readonly DiscordSocketClient _client;

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

        Global.FireAndForget(SyncPeriodically());

        Global.HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0");

        await Task.Delay(-1);
    }

    private static async Task SyncPeriodically()
    {
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(20));
        while (await timer.WaitForNextTickAsync())
        {
            await UserEngine.SyncWithDatabase();
            await GuildEngine.SyncWithDatabase();
            PetEngine.TimedThings();
            await PetEngine.SyncWithDatabase();
        }
    }
}
