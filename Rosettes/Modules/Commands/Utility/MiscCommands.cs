﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Commands.Alarms;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands.Utility;

public class MiscCommands : InteractionModuleBase<SocketInteractionContext>
{
    [MessageCommand("User Profile")]
    public async Task Profile(IMessage message)
    {
        await Profile(message.Author);
    }

    [SlashCommand("profile", "Information about yourself or provided user.")]
    private async Task Profile(IUser? user = null)
    {
        user ??= Context.Interaction.User;

        User dbUser = await UserEngine.GetDbUser(user);
        if (!dbUser.IsValid() || user is not SocketGuildUser guildUser)
        {
            await RespondAsync("There was an error fetching user data from the database.", ephemeral: true);
            return;
        }

        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "User information";
        embed.ThumbnailUrl = guildUser.GetDisplayAvatarUrl(size: 1024);

        embed.AddField("Level", $"Level {dbUser.GetLevel()} ({dbUser.Exp}xp)");
        embed.AddField("Joined Discord", $"<t:{guildUser.CreatedAt.ToUnixTimeSeconds()}:R>", true);
        if (guildUser.JoinedAt is { } guildJoin)
        {
            embed.AddField("Joined Server", $"<t:{guildJoin.ToUnixTimeSeconds()}:R>", true);
        }

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("serverinfo", "Display server information.")]
    public async Task ServerInfo()
    {
        var guild = Context.Guild;
        if (guild == null)
        {
            await RespondAsync("This command won't run in my DM's, silly.");
            return;
        }
        EmbedBuilder embed = await Global.MakeRosettesEmbed();
        embed.Title = $"Information about guild {guild.Name}";
        embed.ThumbnailUrl = guild.IconUrl;

        embed.AddField("Creation date", guild.CreatedAt);
        embed.AddField("Snowflake ID", guild.Id);
        embed.AddField("Members", guild.MemberCount, true);
        embed.AddField("Roles", guild.Roles.Count, true);
        embed.AddField("Owner", guild.Owner.Username);
        embed.AddField("Stickers", guild.Stickers.Count, true);
        embed.AddField("Emoji", guild.Emotes.Count, true);
        
        if (guild.SplashUrl is not null)
        {
            embed.AddField("Splash image URL", $"<{guild.SplashUrl}>");
        }

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("exportemoji", "Generate a ZIP file containing every single emoji in the guild.")]
    public async Task ExportEmoji()
    {
        if (Context.Guild == null)
        {
            await RespondAsync("This command won't run in my DM's, silly.");
            return;
        }

        if (Context.User != Context.Guild.Owner)
        {
            await RespondAsync("This command may only be used by the server owner.", ephemeral: true);
            return;
        }
        
        _ = new EmojiDownloader.EmojiDownloader().DownloadEmojis(Context);
    }

    [SlashCommand("reminder", "Creates a reminder.")]
    public async Task Reminder()
    {
        ModalBuilder modal = new()
        {
            Title = "Create reminder",
            CustomId = "reminderMaker"
        };

        modal.AddTextInput("Time", "time", placeholder: "Number of hours/minutes/days.", maxLength: 10, required: true);
        modal.AddTextInput("Unit", "unit", placeholder: "'Hours', 'Minutes' or 'Days'.", maxLength: 10, required: true);
        modal.AddTextInput("Message", "message", placeholder: "Optional. Message to be included in reminder.", maxLength: 255);

        await RespondWithModalAsync(modal.Build());
    }

    public static async Task FollowUpReminder(int amount, string unit, string message, SocketModal component)
    {
        unit = unit.ToLower();

        if (unit.Contains("minute"))
        {
            // do nothing, since the function receives minutes
        }
        else if (unit.Contains("hour"))
        {
            amount *= 60;
        }
        else if (unit.Contains("days"))
        {
            amount = amount * 60 * 24;
        }
        else
        {
            await component.RespondAsync("Valid units: 'minutes', 'hours', 'days'.", ephemeral: true);
            return;
        }

        if (amount <= 0)
        {
            await component.RespondAsync("Time don't go in that direction.", ephemeral: true);
            return;
        }

        if (message.Length > 255)
        {
            await component.RespondAsync("Sorry, your message is too long. Discord shouldn't even let you do that...", ephemeral: true);
            return;
        }

        var dbUser = await UserEngine.GetDbUser(component.User);
        bool success = await AlarmManager.CreateAlarm(DateTime.Now + TimeSpan.FromMinutes(amount), dbUser, component.Channel, amount, message);

        if (success)
        {
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = "Reminder set.";
            embed.Description = $"A reminder has been set. You will be tagged <t:{((DateTimeOffset)(DateTime.Now + TimeSpan.FromMinutes(amount))).ToUnixTimeSeconds()}:R>";

            embed.AddField("Date and time of alert", $"{(DateTime.Now + TimeSpan.FromMinutes(amount)).ToUniversalTime()} (UTC)");

            await component.RespondAsync(embed: embed.Build());
        }
        else
        {
            await component.RespondAsync("Sorry, there was an error creating your reminder.", ephemeral: true);
        }
    }

    [SlashCommand("feedback", "To send suggestions, feedback, bug reports, complaints or anything else to the bot developers.")]
    public async Task SendFeedback(string text)
    {
        string message = $"Feedback received from {Context.User.Username} (id {Context.User.Id})";
        if (Context.Guild is not null)
        {
            message += $"\nSent from guild {Context.Guild.Name} (id {Context.Guild.Id})";
        }
        message += $"```{text}```";
        Global.GenerateNotification(message);

        await RespondAsync("Your feedback has been sent. All feedback is read and taken into account. If a suggestion you sent is implemented or an issue you pointed out is resolved, you might receive a DM from Rosettes letting you know of this.\n \n If you don't allow DM's from bots, you may not receive anything or get a friend request from `markski.ar` depending on severity.", ephemeral: true);
    }
}