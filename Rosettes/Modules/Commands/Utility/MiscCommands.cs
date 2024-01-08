using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MetadataExtractor.Util;
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
    public async Task Profile(IUser? user = null)
    {
        user ??= Context.Interaction.User;

        User db_user = await UserEngine.GetDBUser(user);
        if (!db_user.IsValid() || user is not SocketGuildUser guildUser)
        {
            await RespondAsync("There was an error fetching user data from the database.", ephemeral: true);
            return;
        }

        EmbedBuilder embed = await Global.MakeRosettesEmbed(db_user);

        embed.Title = "User information";
        embed.ThumbnailUrl = guildUser.GetDisplayAvatarUrl(size: 1024);

        embed.AddField("Level", $"Level {db_user.GetLevel()} ({db_user.Exp}xp)");
        embed.AddField("Joined Discord", $"<t:{guildUser.CreatedAt.ToUnixTimeSeconds()}:R>", true);
        if (guildUser.JoinedAt is DateTimeOffset guildJoin)
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

    [MessageCommand("DL X/Twitter video")]
    public async Task TweetVideoMsg(IMessage message)
    {
        string url = Global.GrabUrlFromText(message.Content);
        if (url != "0") await TweetVideo(url);
        else await RespondAsync("No URL found in this message.", ephemeral: true);
    }

    [SlashCommand("twtvid", "Get the video file of the specified x/twitter post.")]
    public async Task TweetVideo(string tweetUrl)
    {
        bool isX = false;

        int tldEnd = tweetUrl.IndexOf("twitter.com");

        if (tldEnd == -1)
        {
            tldEnd = tweetUrl.IndexOf("x.com");
            isX = true;
        }
        if (tldEnd == -1)
        {
            await RespondAsync("That's not a valid twitter URL.", ephemeral: true);
            return;
        }

        // position this number in the location where the TLD ends.
        if (isX)
            tldEnd += 5;
        else
            tldEnd += 11;

        // remove anything before twitter.com (should also cover links from fxtwitter, sxtwitter etc)
        tweetUrl = tweetUrl[tldEnd..tweetUrl.Length];

        // form a link straight to FxTwitter's direct media backend.
        tweetUrl = $"https://d.fxtwitter.com{tweetUrl}";

        // Let Discord know we are working on it and to not timeout our interaction.
        await DeferAsync();

        EmbedBuilder embed = await Global.MakeRosettesEmbed();

        embed.Title = "Fetching video.";

        EmbedFieldBuilder downloadStatus = new() { Name = "Status", Value = "Downloading from Twitter...", IsInline = true };

        embed.AddField(downloadStatus);

        var mid = await ReplyAsync(embed: embed.Build());

        // store the video locally
        if (!System.IO.Directory.Exists("./temp/twtvid/"))
        {
            System.IO.Directory.CreateDirectory("./temp/twtvid/");
        }

        string fileName = $"./temp/twtvid/{Global.Randomize(20) + 1}.mp4";
        using (HttpResponseMessage response = await Global.HttpClient.GetAsync(tweetUrl))
        {
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                downloadStatus.Value = "Failed to obtain video.";

                await mid.DeleteAsync();
                await FollowupAsync(embed: embed.Build());

                return;
            }

            Stream stream = await response.Content.ReadAsStreamAsync();

            await using var fileStream = new FileStream(fileName, FileMode.Create);

            var cts = new CancellationTokenSource();
            var downloadTask = stream.CopyToAsync(fileStream, cts.Token);

            // cancel if video takes more than 3 seconds to download.
            if (await Task.WhenAny(downloadTask, Task.Delay(3000)) == downloadTask)
            {
                await downloadTask;
            }
            else
            {
                downloadStatus.Value = "Failed. Video took too long to download.";
                embed.AddField("Instead...", $"Have a [Direct link]({tweetUrl}).");

                await mid.DeleteAsync();
                await FollowupAsync(embed: embed.Build());

                cts.Cancel();
                return;
            }
        }

        ulong size;

        // retrieve the video's format.
        await using var checkFileStream = new FileStream(fileName, FileMode.Open);
        FileType fileType = FileTypeDetector.DetectFileType(checkFileStream);
        checkFileStream.Close();

        // ensure the file was downloaded correctly and is either MPEG4 or QuickTime encoded.
        if (!File.Exists(fileName) || fileType is not FileType.QuickTime && fileType is not FileType.Mp4)
        {
            downloadStatus.Value = "Failed. Twitter failed to provide a valid file format.";
            await mid.DeleteAsync();
            await FollowupAsync(embed: embed.Build());

            File.Delete(fileName);
            return;
        }

        downloadStatus.Value = "Uploading to Discord...";

        await mid.ModifyAsync(x => x.Embed = embed.Build());

        size = (ulong)new FileInfo(fileName).Length;

        // check if the guild supports a file this large, otherwise fail.
        if (Context.Guild == null || Context.Guild.MaxUploadLimit > size)
        {
            try
            {
                await FollowupWithFileAsync(fileName);
                _ = mid.DeleteAsync();
            }
            catch
            {
                downloadStatus.Value = "Upload failed.";

                await mid.DeleteAsync();
                await FollowupAsync(embed: embed.Build());
            }
        }
        else
        {
            downloadStatus.Value = "Upload failed. File was too large.";
            embed.AddField("Instead...", $"Have a [Direct link]({tweetUrl}).");
            await FollowupAsync(embed: embed.Build());
            _ = mid.DeleteAsync();
        }
        File.Delete(fileName);
    }

    [SlashCommand("exportemoji", "Generate a ZIP file containing every single emoji in the guild.")]
    public async Task ExportEmoji()
    {
        if (Context.Guild == null)
        {
            await RespondAsync("This command won't run in my DM's, silly.");
            return;
        }

        if (!Global.CheckSnep(Context.User.Id) && Context.User != Context.Guild.Owner)
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

        var dbUser = await UserEngine.GetDBUser(Context.User);

        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "Alarm set.";
        embed.Description = $"An alarm has been. You will be tagged <t:{((DateTimeOffset)(DateTime.Now + TimeSpan.FromMinutes(amount))).ToUnixTimeSeconds()}:R>";

        embed.AddField("Date and time of alert", $"{(DateTime.Now + TimeSpan.FromMinutes(amount)).ToUniversalTime()} (UTC)");

        await RespondAsync(embed: embed.Build());

        AlarmManager.CreateAlarm(DateTime.Now + TimeSpan.FromMinutes(amount), await UserEngine.GetDBUser(Context.User), Context.Channel, amount);
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
        string message;
        message = $"Feedback received from {Context.User.Username} (id {Context.User.Id})";
        if (Context.Guild is not null)
        {
            message += $"\nSent from guild {Context.Guild.Name} (id {Context.Guild.Id})";
        }
        message += $"```{text}```";
        Global.GenerateNotification(message);

        await RespondAsync("Your feedback has been sent. All feedback is read and taken into account. If a suggestion you sent is implemented or an issue you pointed out is resolved, you might receive a DM from Rosettes letting you know of this.\n \n If you don't allow DM's from bots, you may not receive anything or get a friend request from `markski.ar` depending on severity.", ephemeral: true);
    }
}