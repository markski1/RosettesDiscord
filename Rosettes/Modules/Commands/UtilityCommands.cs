﻿using Discord.Interactions;
using Rosettes.Modules.Engine;
using Rosettes.Core;
using Rosettes.Modules.Commands.Alarms;
using Rosettes.Modules.Commands.EmojiDownloader;
using Discord;

namespace Rosettes.Modules.Commands
{
    public class UtilityCommands : InteractionModuleBase<SocketInteractionContext>
    {
        /*
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

            string displayName;
            SocketGuildUser? GuildUser = user as SocketGuildUser;
            if (GuildUser is not null && GuildUser.Nickname is not null)
            {
                displayName = GuildUser.Nickname;
            }
            else
            {
                displayName = Context.User.Username;
            }

            string text = $"***{displayName}#{user.Discriminator}***\n" +
                "```" +
                $"Account created: {user.CreatedAt}\n" +
                $"User ID: {user.Id}\n" +
                $"Experience: {db_user.GetExperience()} (Level {db_user.GetLevel()})\n" +
                $"Currency: {db_user.GetCurrency()}\n" +
                $"```";

            var avatar = user.GetAvatarUrl();
            avatar ??= user.GetDefaultAvatarUrl();

            await ReplyAsync(avatar);
            await ReplyAsync(text);
        }
        */

        [SlashCommand("guildinfo", "Provides information about the guild where it's used.")]
        public async Task GuildInfo() 
        {
            var guild = Context.Guild;
            if (guild == null)
            {
                await RespondAsync("This command won't run in my DM's, silly.");
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
                $"=============="
                ;
            if (guild.IconUrl != null) await RespondAsync(guild.IconUrl);
            await RespondAsync(text);
        }

        [SlashCommand("twtvid", "Get the video file of the specified tweet.")]
        public async Task TweetVideo(string tweetUrl)
        {
            string originalTweet = tweetUrl;
            // From the received URL, generate a URL to the python thing I'm running to parse tweet data.
            if (!tweetUrl.Contains("twitter.com"))
            {
                await RespondAsync("That's not a valid tweet URL.", ephemeral: true);
            }
            // in case such a thing is pasted...
            tweetUrl = tweetUrl.Replace("vxtwitter.com", "gateway.markski.ar:42069");
            tweetUrl = tweetUrl.Replace("fxtwitter.com", "gateway.markski.ar:42069");
            tweetUrl = tweetUrl.Replace("sxtwitter.com", "gateway.markski.ar:42069");
            // normal replace
            tweetUrl = tweetUrl.Replace("twitter.com", "gateway.markski.ar:42069");
            tweetUrl = tweetUrl.Replace("https:/", "http:/");
            tweetUrl = tweetUrl.Replace("<", string.Empty);
            tweetUrl = tweetUrl.Replace(">", string.Empty);

            string? response = null;

            // now try and get a response off the python thing
            try
            {
                using HttpClient _client = new();
                _client.DefaultRequestHeaders.UserAgent.Clear();
                _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Discordbot/2.0; +https://discordapp.com)");
                response = await _client.GetStringAsync(tweetUrl);
            }
            catch
            {
                await RespondAsync("Could not fetch tweet data.");
                return;
            }
            
            if (response is null)
            {
                await RespondAsync("Could not fetch tweet data.");
                return;
            }

            // if we do get something back, it'll be embedded in an HTML, so now do some hacky string scraping things to get the video url out of it.
            if (!response.Contains("twitter:player:stream"))
            {
                await RespondAsync("Could not find a video in that tweet.", ephemeral: true);
                return;
            }

            response = response.Substring(response.IndexOf("twitter:player:stream"), 200);

            int begin = response.IndexOf("https");
            int end = response.IndexOf(".mp4") + 4;
            if (end == -1)
            {
                await RespondAsync("A video was found in that tweet, but I could not extract it.", ephemeral: true);
                return;
            }
            string videoLink = response[begin..end];
            
            // store the video locally
            Random Random = new();
            if (!Directory.Exists("./temp/twtvid/"))
            {
                Directory.CreateDirectory("./temp/twtvid/");
            }
            
            var mid = await ReplyAsync("Downloading the video...");
            string fileName = $"./temp/twtvid/{Random.Next(20) + 1}.mp4";
            using var videoStream = await Global.HttpClient.GetStreamAsync(videoLink);
            using var fileStream = new FileStream(fileName, FileMode.Create);
            await videoStream.CopyToAsync(fileStream);
            fileStream.Close();
            await mid.ModifyAsync(x => x.Content = $"Downloading the video... DONE!\nUploading the video to Discord...");

            ulong size = (ulong)new FileInfo(fileName).Length;

            // check if the guild supports a file this large, otherwise fail.
            if (Context.Guild == null || Context.Guild.MaxUploadLimit > size)
            {
                await RespondWithFileAsync(fileName);
                await mid.DeleteAsync();
            } 
            else
            {
                await mid.ModifyAsync(x => x.Content = $"Downloading the video... DONE!\nUploading the video to Discord... FAILED!");
                await RespondAsync("Sorry, video was too large to be uploaded...\nInstead, have a direct link:");
                EmbedBuilder embed = new();
                embed.AddField("Download: ", $"[Direct link]({videoLink})");
                await ReplyAsync(embed: embed.Build());
            }
        }

        [SlashCommand("exportallemoji", "Generate a ZIP file containing every single emoji in the guild.")]
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
           
            if (DownloadEmoji.CheckIsDownloading())
            {
                await RespondAsync("There's currently an emoji export being done, likely in another server. Try again in a bit.", ephemeral: true);
            }
            else
            {
                await RespondAsync("Retrieving emoji...");
                _ = DownloadEmoji.DoTheThing(Context);
            }
        }

        [SlashCommand("arc", "Provide a quick archive.is link for a provided URL.")]
        public async Task Archive(string url)
        {
            await RespondAsync($"<https://archive.is/submit/?url={url}>");
        }

        [SlashCommand("alarm", "Sets an alarm to ring after a given period of time (by default, in minutes).")]
        public async Task Alarm(int minutes)
        {
            if (AlarmManager.CheckUserHasAlarm(Context.User))
            {
                await RespondAsync("You already have an alarm set! Only one alarm per user. You may also cancel your current alarm with $cancelalarm.", ephemeral: true);
                return;
            }

            if (minutes <= 0)
            {
                await RespondAsync("Time don't go in that direction.");
                return;
            }

            await RespondAsync($"Okay! I will tag you in {minutes} minute{((minutes != 1) ? 's' : null)}");

            AlarmManager.CreateAlarm((DateTime.Now + TimeSpan.FromSeconds(minutes)), await UserEngine.GetDBUser(Context.User), Context.Channel, minutes);
        }

        [SlashCommand("cancelalarm", "Cancels your current alarm.")]
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
            message = $"Feedback received from {Context.User.Username}#{Context.User.Discriminator} (id {Context.User.Id})";
            if (Context.Guild is not null)
            {
                message += $"\nSent from guild {Context.Guild.Name} (id {Context.Guild.Id})";
            }
            message += $"```{text}```";
            Global.GenerateNotification(message);

            await RespondAsync("Your feedback has been sent. All feedback is read and taken into account. If a suggestion you sent is implementer or an issue you pointed out is resolved, you might receive a DM from Rosettes letting you know of this.\n \n If you don't allow DM's from bots, you may not receive anything or get a friend request from Markski#7243 depending on severity.", ephemeral: true);
        }
    }
}