using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
	[Group("music", "Music control commands")]
	public class MusicCommands : InteractionModuleBase<SocketInteractionContext>
	{
		[SlashCommand("play", "Joins VC and plays the specified song. Can be a URL or a search term.")]
		public async Task PlayMusic(string urlOrSearch)
		{
			if (await CheckMusicConditions() == false) return;
			if (Context.User is not SocketGuildUser _socketUser || Context.User is not IVoiceState _voiceState || Context.Channel is not ITextChannel _textChannel)
			{
				await Context.Channel.SendMessageAsync("Something went wrong.");
				return;
			}
			if (await CheckMusicConditions() == false) return;

			var dbUser = await UserEngine.GetDBUser(Context.User);
			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
			embed.Title = "Music playback";

			embed.Description = "Initializing music playback, please wait...";

            await RespondAsync(embed: embed.Build());

            try
			{
				embed.Description = await MusicEngine.PlayAsync(_socketUser, Context.Guild, _voiceState, _textChannel, urlOrSearch);
			}
			catch
			{
				embed.Description = "Sorry, there was an error initializing the music player.";
			}

			var response = await GetOriginalResponseAsync();

			try
			{
				await response.ModifyAsync(x => x.Embed = embed.Build());
                await response.ModifyAsync(x => x.Components = GetMusicButtons());
            }
			catch
			{
				await Context.User.SendMessageAsync("I don't have permissions in that channel to execute the music player!");
			}

			MusicEngine.SetChannelPlayer(Context.Channel, response);
		}

		[SlashCommand("stop", "Stops playing music.")]
		public async Task StopMusic()
		{
			if (await CheckMusicConditions() == false) return;

			var dbUser = await UserEngine.GetDBUser(Context.User);
			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
			embed.Title = "Stop playback";
			embed.Description = await MusicEngine.StopAsync(Context.Guild);

			await RespondAsync(embed: embed.Build());
			MusicEngine.SetChannelPlayer(Context.Channel, await GetOriginalResponseAsync());
		}

		[SlashCommand("skip", "Skip to the next song in the queue.")]
		public async Task SkipMusic()
		{
			if (await CheckMusicConditions() == false) return;

			var dbUser = await UserEngine.GetDBUser(Context.User);
			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
			embed.Title = "Skip song";
			embed.Description = await MusicEngine.SkipTrackAsync(Context.Guild);

			await RespondAsync(embed: embed.Build(), components: GetMusicButtons());
			MusicEngine.SetChannelPlayer(Context.Channel, await GetOriginalResponseAsync());
		}

		[SlashCommand("toggle", "Pauses and resumes the currently playing song.")]
		public async Task ToggleMusic()
		{
			if (await CheckMusicConditions() == false) return;

			var dbUser = await UserEngine.GetDBUser(Context.User);
			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
			embed.Title = "Toggle playback";
			embed.Description = await MusicEngine.ToggleAsync(Context.Guild);

			await RespondAsync(embed: embed.Build(), components: GetMusicButtons());
			MusicEngine.SetChannelPlayer(Context.Channel, await GetOriginalResponseAsync());
		}

		[SlashCommand("leave", "Make the bot leave VC.")]
		public async Task LeaveMusic()
		{
			if (await CheckMusicConditions() == false) return;
			var dbUser = await UserEngine.GetDBUser(Context.User);
			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
			embed.Title = "Leave VC";
			embed.Description = await MusicEngine.LeaveAsync(Context.Guild);

			await RespondAsync(embed: embed.Build());
		}


		public async Task<bool> CheckMusicConditions()
		{
			if (Context.Guild == null)
			{
				await RespondAsync("This command won't run in my DM's, silly.");
				return false;
			}
			if (!Global.CanSendMessage(Context))
			{
				await RespondAsync("I don't have access to this channel. Please let an admin know, or try using me in other channel.", ephemeral: true);
				return false;
			}
			var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
			if (!dbGuild.AllowsMusic())
			{
				await RespondAsync("Sorry, but the guild admins have disabled the use of music commands.", ephemeral: true);
				return false;
			}
			return true;
		}

		public static MessageComponent GetMusicButtons()
		{
			var buttons = new ActionRowBuilder();

			buttons.WithButton(label: "Play/Pause", customId: "music_toggle", style: ButtonStyle.Success);
			buttons.WithButton(label: "Skip", customId: "music_skip", style: ButtonStyle.Primary);
			buttons.WithButton(label: "Stop", customId: "music_stop", style: ButtonStyle.Danger);
			buttons.WithButton(label: "Add song", customId: "music_add", style: ButtonStyle.Secondary);

			ComponentBuilder comps = new();

			comps.AddRow(buttons);

			return comps.Build();
		}
	}
}
