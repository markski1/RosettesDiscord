using Discord;
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

    [SlashCommand("alarm", "Sets an alarm to ring after a given period of time (by default, in minutes).")]
    public async Task Alarm([Summary("amount", "Amount of time until alarm sounds. In minutes unless specified otherwise.")] int amount, [Summary("unit", "Unit of time for the amount of time provided. Can be minutes/hours/days")] string unit = "minute")
    {
        if (AlarmManager.CheckUserHasAlarm(Context.User))
        {
            await RespondAsync("You already have an alarm set! Only one alarm per user. You may also cancel your current alarm with /cancelalarm.", ephemeral: true);
            return;
        }

        if (unit.ToLower().Contains("minute"))
        {
            // nothing as the function receives minutes
        }
        else if (unit.ToLower().Contains("hour"))
        {
            amount *= 60;
        }
        else if (unit.ToLower().Contains("day"))
        {
            amount = amount * 60 * 24;
        }
        else
        {
            await RespondAsync("Valid units: 'minutes', 'hours', 'days'.", ephemeral: true);
        }

        if (amount <= 0)
        {
            await RespondAsync("Time don't go in that direction.", ephemeral: true);
            return;
        }

        var dbUser = await UserEngine.GetDbUser(Context.User);
        bool success = await AlarmManager.CreateAlarm(DateTime.Now + TimeSpan.FromMinutes(amount), dbUser, Context.Channel, amount);

        if (success)
        {
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = "Alarm set.";
            embed.Description = $"An alarm has been set. You will be tagged <t:{((DateTimeOffset)(DateTime.Now + TimeSpan.FromMinutes(amount))).ToUnixTimeSeconds()}:R>";

            embed.AddField("Date and time of alert", $"{(DateTime.Now + TimeSpan.FromMinutes(amount)).ToUniversalTime()} (UTC)");

            await RespondAsync(embed: embed.Build());
        }
        else
        {
            await RespondAsync("Sorry, there was an error setting an alarm.", ephemeral: true);
        }
    }

    [SlashCommand("alarm-cancel", "Cancels your current alarm.")]
    public async Task CancelAlarm()
    {
        if (!AlarmManager.CheckUserHasAlarm(Context.User))
        {
            await RespondAsync("You don't have any alarm set.");
            return;
        }

        Alarm? alarm = AlarmManager.GetUserAlarm(Context.User);
        if (alarm != null)
        {
            AlarmManager.DeleteAlarm(alarm);
            await RespondAsync("Your alarm has been cancelled.");
        }
        else
        {
            await RespondAsync("There was an error deleting your alarm.");
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