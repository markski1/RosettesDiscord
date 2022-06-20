using Discord;
using Discord.Commands;
using System.IO.Compression;
using Rosettes.Modules.Engine;
using Rosettes.Core;
using Rosettes.Modules.Commands.Alarms;
using Rosettes.Modules.Commands.Emoji;

namespace Rosettes.Modules.Commands
{
    [Summary("General purpose commands")]
    public class UtilityCommands : ModuleBase<SocketCommandContext>
    {
        [Command("myinfo")]
        [Summary("Provides information about yourself.")]
        public async Task MyInfo()
        {
            var user = Context.Message.Author;
            User db_user = await UserEngine.GetDBUser(user);
            if (!db_user.IsValid())
            {
                await ReplyAsync("There was an error fetching your data from the database.");
                return;
            }
            string text = $"***{user.Username}#{user.Discriminator}***\n" +
                "```" +
                $"Account created: {user.CreatedAt}\n" +
                $"User ID: {user.Id}\n" +
                $"Experience: {db_user.GetExperience()} (Level {db_user.GetLevel()})\n" +
                $"Currency: {db_user.GetCurrency()}\n" +
                $"```";

            var avatar = user.GetAvatarUrl();
            if (avatar == null) avatar = user.GetDefaultAvatarUrl();

            await ReplyAsync(avatar);
            await ReplyAsync(text);
        }

        [Command("guildinfo")]
        [Summary("Provides information about the guild where it's used.")]
        public async Task GuildInfo() 
        {
            var guild = Context.Guild;
            if (guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            string text = "```" +
                $"Information about guild {guild.Name}\n" +
                $"==============\n" +
                $"Guild created :  {guild.CreatedAt}\n" +
                $"Guild ID      :  {guild.Id}\n" +
                $"Members       :  {guild.MemberCount}\n" +
                $"Owner         :  {guild.Owner.DisplayName}\n" +
                $"Roles         :  {guild.Roles.Count}\n" +
                $"Stickers      :  {guild.Stickers.Count}\n" +
                $"Emoji         :  {guild.Emotes.Count}\n" +
                $"==============\n\n" +
                $"Top users by experience:\n" +
                $"==============\n"
                ;
            text += await UserEngine.GetTopUsersString(guild);
            text += $"==============```";
            if (guild.IconUrl != null) await ReplyAsync(guild.IconUrl);
            await ReplyAsync(text);
        }

        [Command("twtvid")]
        [Summary("Get the video file of the specified tweet.\nExample usage: '$twtvid <tweet url>'")]
        public async Task TweetVideoAsync(string tweetUrl = "UNSPECIFIED")
        {
            if (tweetUrl == "UNSPECIFIED")
            {
                await ReplyAsync($"Usage: `{Settings.Prefix}twtvid [tweet url]`");
                return;
            }
            if (!tweetUrl.Contains("vxtwitter.com"))
            {
                if (!tweetUrl.Contains("twitter.com"))
                {
                    await ReplyAsync("That's not a valid tweet URL.");
                }
                tweetUrl = tweetUrl.Replace("twitter.com", "vxtwitter.com");

            }
            tweetUrl = tweetUrl.Replace("<", string.Empty);
            tweetUrl = tweetUrl.Replace(">", string.Empty);
            using HttpClient _client = new();
            _client.DefaultRequestHeaders.UserAgent.Clear();
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Discordbot/2.0; +https://discordapp.com)");
            var response = await _client.GetStringAsync(tweetUrl);

            if (response is null)
            {
                await ReplyAsync("Could not fetch tweet data.");
                return;
            }

            if (!response.Contains("twitter:player:stream"))
            {
                await ReplyAsync("Could not find a video in that tweet.");
                return;
            }

            response = response.Substring(response.IndexOf("twitter:player:stream"), 200);

            int begin = response.IndexOf("https");
            int end = response.IndexOf(".mp4") + 4;
            if (end == -1)
            {
                await ReplyAsync("A video was found in that tweet, but I could not extract it.");
                return;
            }
            string videoLink = response[begin..end];
            await ReplyAsync(videoLink);
        }

        [Command("exportallemoji")]
        [Summary("Generate a ZIP file containing every single emoji in the guild where it's used. May only be used by the owner.")]
        public async Task ExportEmojiAsync()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }

            if (!Global.CheckSnep(Context.User.Id) && Context.User != Context.Guild.Owner)
            {
                await ReplyAsync("This command may only be used by the server owner or by the boss snep.");
                return;
            }
           
            if (DownloadEmoji.CheckIsDownloading())
            {
                await ReplyAsync("There's currently an emoji export being done, likely in another server. Try again in a bit.");
            }
            else
            {
                await ReplyAsync("Retrieving emoji...");
                _ = DownloadEmoji.DoTheThing(Context);
            }
        }

        [Command("arc")]
        [Summary("Provide a quick archive.is link for a provided URL.\nExample usage: '$arc example.com'")]
        public async Task ArchiveAsync(string url = "empty")
        {
            if (url == "empty")
            {
                await ReplyAsync($"Usage: `{Settings.Prefix}arc <url>`");
                return;
            }
            await ReplyAsync($"<https://archive.is/submit/?url={url}>");
        }

        [Command("alarm")]
        [Summary("Sets an alarm to ring after a given period of time.\nExample usage: '$alarm 30 m' (Mentions you in 30 minutes) | h = hours, m = minutes, s = seconds")]
        public async Task AlarmAsync(int amount = -69420, char time = 'n')
        {
            if (AlarmManager.CheckUserHasAlarm(Context.User))
            {
                await ReplyAsync("You already have an alarm set! Only one alarm per user. You may also cancel your current alarm with $cancelalarm.");
                return;
            }

            int seconds;
            string unit;
            if (amount == -69420)
            {
                await ReplyAsync($"Usage: `{Settings.Prefix}alarm <amount> <time specifier>`\nwhere a time specifier can be h for hours, m for minutes or s for seconds.");
            }
            if (amount <= 0)
            {
                await ReplyAsync("Time don't go in that direction.");
                return;
            }
            switch (time) {
                case 's':
                    seconds = amount;
                    unit = "second";
                    break;

                case 'm':
                    seconds = amount * 60;
                    unit = "minute";
                    break;
                case 'h':
                    seconds = amount * 3600;
                    unit = "hour";
                    break;
                default:
                    await ReplyAsync($"Usage: `{Settings.Prefix}alarm <amount> <time specifier>`\nwhere a time specifier can be h for hours, m for minutes or s for seconds.");
                    return;
            }

            await ReplyAsync($"Okay! I will tag you in {amount} {unit}{((amount != 1) ? 's' : null)}");

            AlarmManager.CreateAlarm((DateTime.Now + TimeSpan.FromSeconds(seconds)), await UserEngine.GetDBUser(Context.User), Context.Channel, seconds);
        }

        [Command("cancelalarm")]
        [Summary("Cancels your current alarm.")]
        public async Task CancelAlarmAsync()
        {
            if (!AlarmManager.CheckUserHasAlarm(Context.User))
            {
                await ReplyAsync("You don't have any alarm set.");
                return;
            }

            Alarm? alarm = AlarmManager.GetUserAlarm(Context.User);
            if (alarm != null)
            {
                AlarmManager.DeleteAlarm(alarm);
                await ReplyAsync("Your alarm has been cancelled.");
            }
            else
            {
                await ReplyAsync("There was an error deleting your alarm.");
            }
        }
    }
}