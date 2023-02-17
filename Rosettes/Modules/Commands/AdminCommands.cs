using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
	public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
	{
		[SlashCommand("makepoll", "Provides an UI to create your own custom poll.")]
		public async Task MakePoll()
		{
			if (Context.Guild is null)
			{
				await RespondAsync("You may only create polls within a guild.");
				return;
			}

			ModalBuilder modal = new()
			{
				Title = "Poll maker",
				CustomId = "pollMaker"
			};

			modal.AddTextInput("Question", "question", TextInputStyle.Paragraph, placeholder: "Ask a question...", minLength: 5, maxLength: 250);

			modal.AddTextInput("Option 1", "option1", required: true, minLength: 1, maxLength: 100);
			modal.AddTextInput("Option 2", "option2", required: true, minLength: 1, maxLength: 100);
			modal.AddTextInput("Option 3", "option3", required: false, minLength: 1, maxLength: 100);
			modal.AddTextInput("Option 4", "option4", required: false, minLength: 1, maxLength: 100);

			await RespondWithModalAsync(modal.Build());
		}

		public async static Task FollowUpPoll(string question, string option1, string option2, string option3, string option4, SocketModal component)
		{
			// prevent option 4 with no option 3
			if (option3 == string.Empty && option4 != string.Empty)
			{
				option3 = option4;
				option4 = string.Empty;
			}

			EmbedBuilder embed = await Global.MakeRosettesEmbed();
			embed.Title = question;
			embed.Footer = new EmbedFooterBuilder() { Text = "Choose one option, it cannot be changed.\nVoting is anonymous." };

			var comps = new ComponentBuilder();

			comps.WithButton(label: $"{option1} - 0 votes", customId: "1", row: 0);
			comps.WithButton(label: $"{option2} - 0 votes", customId: "2", row: 1);

			if (option3 != string.Empty)
			{
				comps.WithButton(label: $"{option3} - 0 votes", customId: "3", row: 2);
			}
			else
			{
				option3 = "NOT_PROVIDED";
			}
			if (option4 != string.Empty)
			{
				comps.WithButton(label: $"{option4} - 0 votes", customId: "4", row: 3);
			}
			else
			{
				option4 = "NOT_PROVIDED";
			}

			await component.RespondAsync(embed: embed.Build(), components: comps.Build());

			ulong id = (await component.GetOriginalResponseAsync()).Id;

			bool success = await PollEngine.AddPoll(id, question, option1, option2, option3, option4);

			if (!success)
			{
				await component.DeleteOriginalResponseAsync();
				await component.RespondAsync("Sorry, there was an error creating this poll.", ephemeral: true);
			}
		}

		[SlashCommand("setautorole", "Sets the desired autoroles where used. Must first be created in the web panel.")]
		public async Task SetAutoRoles(uint code)
		{
			if (Context.Guild is null)
			{
				await RespondAsync("This command must run in a guild.");
				return;
			}
			if (!Global.CheckSnep(Context.User.Id) && Context.User != Context.Guild.Owner)
			{
				await RespondAsync("This command may only be used by the server owner or a Rosettes developer.", ephemeral: true);
				return;
			}

			await AutoRolesEngine.SyncWithDatabase();

			var roles = AutoRolesEngine.GetRolesByCode(code, Context.Guild.Id);

			if (roles is null || !roles.Any())
			{
				await RespondAsync("Error. Please make sure you're using the right code in the right guild.", ephemeral: true);
				return;
			}

			List<Emoji> emojis = new();

			string text = "";

			var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
			var restGuild = await dbGuild.GetDiscordRestReference();
			var socketGuild = dbGuild.GetDiscordSocketReference();

			EmbedBuilder embed = new()
			{
				Color = Color.DarkPurple,
				Title = AutoRolesEngine.GetNameFromCode(code),
				Description = " "
			};

			foreach (var role in roles)
			{
				emojis.Add(new Emoji(role.Emote));
				string roleName = "";
				if (socketGuild is not null && socketGuild.GetRole(role.RoleId) is not null)
				{
					roleName = socketGuild.GetRole(role.RoleId).Mention;
				} else if (restGuild is not null && restGuild.GetRole(role.RoleId) is not null)
				{
					roleName = restGuild.GetRole(role.RoleId).Mention;
				}
				text += $"{role.Emote} - {roleName}\n\n";
			}

			embed.AddField("Available roles: ", text);

			var mid = await ReplyAsync(embed: embed.Build());

			await mid.AddReactionsAsync(emojis);

			await GuildEngine.UpdateGuild(dbGuild);

			await AutoRolesEngine.UpdateGroupMessageId(code, mid.Id);

			await RespondAsync("Autoroles message created. If you get permissions errors, remember the following:\n\n1. Make sure you did not remove the 'Manage roles' permission when you invited Rosettes into your server.\n2. Make sure the role \"Rosettes\" is higher in the list of roles than the ones which can be chosen.", ephemeral: true);
		}

		[SlashCommand("setlogchan", "Sets the channel where user join/left is sent. Use 'disable: true' to disable.")]
		public async Task SetLogChan(string disable = "false")
		{
			if (Context.Guild.OwnerId != Context.User.Id && !Global.CheckSnep(Context.User.Id))
			{
				await RespondAsync("This command may only be used by the server owner or a Rosettes developer.", ephemeral: true);
			}

			var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);

			if (disable == "false")
			{
				dbGuild.LogChannel = Context.Channel.Id;

				await RespondAsync("Got it, Rosettes will now report joins and leaves in this channel.");
			}
			else
			{
				dbGuild.LogChannel = 0;

				await RespondAsync("Got it, Rosettes will no longer report joins and leaves.");
			}
		}

		[SlashCommand("setfarmchan", "Sets the channel where Farm/Fishing commands may be used. Use 'disable' to disable.")]
		public async Task SetFarmChan(string disable = "false")
		{
			if (Context.Guild.OwnerId != Context.User.Id && !Global.CheckSnep(Context.User.Id))
			{
				await RespondAsync("This command may only be used by the server owner or a Rosettes developer.", ephemeral: true);
			}

			var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);

			if (disable == "false")
			{
				dbGuild.RpgChannel = Context.Channel.Id;

				await RespondAsync("Got it, Rosettes will now only allow Farm/Fishing commands in this channel.");
			}
			else
			{
				dbGuild.RpgChannel = 0;

				await RespondAsync("Got it, Rosettes will now allow Farm/Fishing commands anywhere in the guild (unless disabled from the web panel).");
			}
		}

		[SlashCommand("settings", "Change guild settings")]
		public async Task ShowSettings()
		{
			if (Context.Guild.OwnerId != Context.User.Id && !Global.CheckSnep(Context.User.Id))
			{
				await RespondAsync("This command may only be used by the server owner or a Rosettes developer.", ephemeral: true);
			}

			try
			{
				await Context.Channel.GetPinnedMessagesAsync();
			}
			catch
			{
				await RespondAsync("I don't have access to this channel.", ephemeral: true);
				return;
			}

			var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);

			var component = AdminHelper.GetGuildSettingsButtons(dbGuild);

			EmbedBuilder embed = await Global.MakeRosettesEmbed();

			embed.Title = $"{dbGuild.NameCache} | Settings";

			embed.Footer = new EmbedFooterBuilder() { Text = "You may also use the web panel for more comprehensive settings,\nsuch as setting up AutoRoles.\nhttps://snep.markski.ar/rosettes" };

			await RespondAsync(embed: embed.Build(), components: component);

			dbGuild.cacheSettingsMsg = await GetOriginalResponseAsync();
		}
	}

	public static class AdminHelper
	{
		public static MessageComponent GetGuildSettingsButtons(Guild dbGuild)
		{
			string enabledText;

			ActionRowBuilder firstRow = new();
			enabledText = (dbGuild.MessageAnalysis()) ? "Enabled" : "Disabled";
			firstRow.WithButton($"Message parsing: {enabledText}", "toggle_msg");

			enabledText = (dbGuild.AllowsMusic()) ? "Enabled" : "Disabled";
			firstRow.WithButton($"Music commands: {enabledText}", "toggle_music");

			ActionRowBuilder secondRow = new();
			enabledText = (dbGuild.AllowsRPG()) ? "Enabled" : "Disabled";
			secondRow.WithButton($"Farm minigame: {enabledText}", "toggle_farm");

			enabledText = (dbGuild.AllowsRandom()) ? "Enabled" : "Disabled";
			secondRow.WithButton($"Gambling commands: {enabledText}", "toggle_gambling");

			enabledText = (dbGuild.MonitorsVC()) ? "Enabled" : "Disabled";
			secondRow.WithButton($"Announce VC joins/quit: {enabledText}", "toggle_monitorvc");

			ActionRowBuilder fourthRow = new();
			fourthRow.WithButton("/setfarmchan - Sets channel for farm minigame", "null_1", style: ButtonStyle.Secondary, disabled: true);
			fourthRow.WithButton("/setlogchan - Sets channel to report users joining and leaving the guild", "null_2", style: ButtonStyle.Secondary, disabled: true);

			ComponentBuilder comps = new();

			comps
				.AddRow(firstRow)
				.AddRow(secondRow)
			 // .AddRow(thirdRow)
				.AddRow(fourthRow);


			return comps.Build();
		}

		public static async Task ChangeSettings(SocketMessageComponent component)
		{
			if (component.GuildId is ulong guild_id)
			{
				var dbGuild = GuildEngine.GetDBGuildById(guild_id);
				var guildRef = dbGuild.GetDiscordSocketReference();
				if (guildRef is null) return;
				if (guildRef.OwnerId != component.User.Id && !Global.CheckSnep(component.User.Id))
				{
					await component.RespondAsync("This command may only be used by the server owner or a Rosettes developer.", ephemeral: true);
				}

				string action = component.Data.CustomId;

				switch (action) {
					case "toggle_msg":
						dbGuild.ToggleSetting(0);
						break;
					case "toggle_music":
						dbGuild.ToggleSetting(1);
						break;
					case "toggle_farm":
						dbGuild.ToggleSetting(4);
						break;
					case "toggle_gambling":
						dbGuild.ToggleSetting(2);
						break;
					case "toggle_monitorvc":
						dbGuild.ToggleSetting(5);
						break;
				}

				var buttonComponent = AdminHelper.GetGuildSettingsButtons(dbGuild);

				try
				{
					if (dbGuild.cacheSettingsMsg is not null)
					{
						await component.DeferAsync(ephemeral: true);
						await dbGuild.cacheSettingsMsg.ModifyAsync(x => x.Components = buttonComponent);
					}
					else
					{
						await component.RespondAsync(components: buttonComponent);
						dbGuild.cacheSettingsMsg = await component.GetOriginalResponseAsync();
					}
				}
				catch
				{
					await component.RespondAsync("There was an error, I might not have access to the channel.", ephemeral: true);
				}
			}
		}
	}
}