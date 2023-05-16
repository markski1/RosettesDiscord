using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Modules.Engine;
using Microsoft.Extensions.DependencyInjection;
using Victoria.Node;
using Microsoft.Extensions.Logging.Abstractions;
using Rosettes.Managers;
using Rosettes.Modules.Engine.Guild;
using Rosettes.Modules.Engine.Minigame;
using System.Diagnostics;

namespace Rosettes.Core
{
    public class RosettesMain
	{
		private readonly DiscordSocketClient Client;
		private readonly InteractionService Commands;
		private readonly System.Timers.Timer TenMinutyTimer = new();

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

			// FiveMinutyTimer(); defined below, runs every 10 minutes, or 600 seconds
			TenMinutyTimer.Elapsed += TenMinutyThings;
			TenMinutyTimer.Interval = 600000;
			TenMinutyTimer.AutoReset = true;
			TenMinutyTimer.Enabled = true;

			// Done! Now keep this task blocked forever to avoid the bot from closing.
			await Task.Delay(-1);
		}

		public void TenMinutyThings(object? source, System.Timers.ElapsedEventArgs e)
		{
			using Process proc = Process.GetCurrentProcess();
			TimeSpan elapsed = DateTime.Now - proc.StartTime;

			// Restart every 3 days.
			if (elapsed > TimeSpan.FromHours(72)) {
				_ = HaltOrRestart(true);
			}
			else
			{
				UserEngine.SyncWithDatabase();
				GuildEngine.SyncWithDatabase();
				PetEngine.TimedThings();
				PetEngine.SyncWithDatabase();
			}
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

			if (restart) {
				try
				{
					// In the machine where Rosettes runs, the startRosettes.sh script located one directory above
					// properly initializes certain files and starts Rosettes as a background process through nohup <whatever> &
					// I find this more convenient than running it as a systemd service for reasons I don't care to discuss here.
					int runSuccess = await Global.RunBash("../startRosettes.sh");
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
}
