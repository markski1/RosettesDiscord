using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Modules.Engine;
using Microsoft.Extensions.DependencyInjection;
using Victoria.Node;
using Microsoft.Extensions.Logging.Abstractions;
using Rosettes.Managers;

namespace Rosettes.Core
{
    public class RosettesMain
	{
		private readonly DiscordSocketClient Client;
		private readonly InteractionService Commands;
		private readonly System.Timers.Timer FiveMinutyTimer = new();

		public RosettesMain()
		{
			InteractionServiceConfig interaction_config = new()
			{
				DefaultRunMode = RunMode.Async,
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
			Commands = new(Client, interaction_config);


			ServiceCollection collection = new();

			collection.AddSingleton(Client);
			collection.AddSingleton(Commands);
			collection.AddSingleton<InteractionManager>();

			ServiceManager.SetProvider(collection);

			try
			{
				MusicEngine.SetMusicEngine(Client);
			}
			catch (Exception ex)
			{
				Global.GenerateErrorMessage("RosettesMain-LavalinkStartFail", $"{ex.Message}");
			}
		}

		public async Task MainAsync()
		{
			// Identify with the token and connect to discord.
			await Client.LoginAsync(TokenType.Bot, Settings.Token);
			await Client.StartAsync();

			// start thy stuff
			await EventManager.SetupAsync();

			// FiveMinutyTimer(); defined below, runs every 5 minutes, or 300 seconds
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
		}
	}        
}
