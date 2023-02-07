﻿using Discord.Interactions;
using Rosettes.Modules.Engine;
using Rosettes.Core;
using Rosettes.Modules.Commands.Alarms;
using Discord;
using MetadataExtractor.Util;
using static System.Reflection.Metadata.BlobBuilder;
using System.Security.Cryptography;
using MetadataExtractor;

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

        [MessageCommand("Get Metadata")]
        public async Task GetMetadata(IMessage message)
        {
            var dbUser = await UserEngine.GetDBUser(Context.User);
            var embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = "Metadata extractor";

            if (!message.Attachments.Any())
            {
                embed.Description = "No attachments were found in this message.";
                await RespondAsync(embed: embed.Build(), ephemeral: true);
                return;
            }

            int count = 1;

            await DeferAsync();

            if (!System.IO.Directory.Exists("./temp/metadata/"))
            {
                System.IO.Directory.CreateDirectory("./temp/metadata/");
            }

            Random Random = new();

            string resultsFileName = $"./temp/metadata/Results-{Random.Next(20) + 1}.txt";

            bool anySuccess = false;

            if (File.Exists(resultsFileName))
            {
                File.Delete(resultsFileName);
            }

            FileStream logFs = File.Create(resultsFileName);

            Global.WriteToFs(ref logFs, "[GENERATED BY ROSETTES / https://markski.ar/rosettes]\nMetadata results:\n\n");

            foreach (var attachment in message.Attachments)
            {
                if (attachment.Size > 52428800)
                {
                    embed.AddField($"Attachment {count}", "Sorry, this attachment is too large. (> 50MB)", true);
                    Global.WriteToFs(ref logFs, $"Attachment {count} - File too large for analysis.\n\n");
                }
                else
                {
                    string details = "";

                    string fileName = $"./temp/metadata/{Random.Next(20) + 1}";
                    using var attachmentStream = await Global.HttpClient.GetStreamAsync(attachment.Url);
                    using var fileStream = new FileStream(fileName, FileMode.Create);
                    await attachmentStream.CopyToAsync(fileStream);
                    fileStream.Close();

                    IEnumerable<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(fileName);

                    foreach(var directory in directories)
                    {
                        foreach(var tag in directory.Tags)
                        {
                            string spaceStr = "";
                            int space = 30 - tag.Name.Length;
                            for (int i = 0; i < space; i++)
                            {
                                spaceStr += " ";
                            }
                            details += $"{tag.Name}:{spaceStr}{tag.Description}\n";
                        }
                    }

                    embed.AddField($"Attachment {count}", "Succesfully analyzed", true);
                    Global.WriteToFs(ref logFs, $"=====\nAttachment {count}\n=====\n{details}=====\n\n");
                    anySuccess = true;
                }
                count++;
            }

            logFs.Close();

            if (anySuccess) embed.Footer = new EmbedFooterBuilder() { Text = "Check the attached text file with the metadata results." };

            await ModifyOriginalResponseAsync(x => x.Embed = embed.Build());

            if (anySuccess) await Context.Channel.SendFileAsync(resultsFileName);
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
            embed.AddField("Owner", guild.Owner.Username + "#" + guild.Owner.Discriminator);
            embed.AddField("Stickers", guild.Stickers.Count, true);
            embed.AddField("Emoji", guild.Emotes.Count, true);
            if (guild.SplashUrl is not null)
            {
                embed.AddField("Splash image URL", $"<{guild.SplashUrl}>");
            }

            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("twtvid", "Get the video file of the specified tweet.")]
        public async Task TweetVideo(string tweetUrl)
        {
            await TweetVideoFunc(tweetUrl);
            return;
        }

        [MessageCommand("DL Twitter video")]
        public async Task TweetVideoMsg(IMessage message)
        {
            string url = Global.GrabURLFromText(message.Content);
            if (url != "0") await TweetVideoFunc(url);
            else await RespondAsync("No URL found in this message.", ephemeral: true);
        }

        public async Task TweetVideoFunc(string tweetUrl)
        {
            string originalTweet = tweetUrl;
            // From the received URL, generate a URL to the python thing I'm running to parse tweet data.
            if (!tweetUrl.Contains("twitter.com"))
            {
                await RespondAsync("That's not a valid tweet URL.", ephemeral: true);
                return;
            }
            // in case such a thing is pasted...
            tweetUrl = tweetUrl.Replace("https://vxtwitter.com", "https://d.fxtwitter.com");
            tweetUrl = tweetUrl.Replace("https://fxtwitter.com", "https://d.fxtwitter.com");
            tweetUrl = tweetUrl.Replace("https://sxtwitter.com", "https://d.fxtwitter.com");
            // normal replace
            tweetUrl = tweetUrl.Replace("https://twitter.com", "https://d.fxtwitter.com");
            // remove non-embed gt and lt signs if applicable
            tweetUrl = tweetUrl.Replace("<", string.Empty);
            tweetUrl = tweetUrl.Replace(">", string.Empty);

            EmbedBuilder embed = await Global.MakeRosettesEmbed();

            embed.Title = "Exporting twitter video.";

            EmbedFieldBuilder downloadField = new() { Name = "Video download.", Value = "In progress...", IsInline = true };

            EmbedFieldBuilder uploadField = new() { Name = "Video upload.", Value = "Waiting..." };

            embed.AddField(downloadField);
            embed.AddField(uploadField);

            var mid = await ReplyAsync(embed: embed.Build());

            // store the video locally
            Random Random = new();
            if (!System.IO.Directory.Exists("./temp/twtvid/"))
            {
                System.IO.Directory.CreateDirectory("./temp/twtvid/");
            }

            string fileName = $"./temp/twtvid/{Random.Next(20) + 1}.mp4";
            using var videoStream = await Global.HttpClient.GetStreamAsync(tweetUrl);
            using var fileStream = new FileStream(fileName, FileMode.Create);
            await videoStream.CopyToAsync(fileStream);
            fileStream.Close();
            using var checkFileStream = new FileStream(fileName, FileMode.Open);
            FileType fileType = FileType.Unknown;
            if (checkFileStream is not null)
            {
                fileType = FileTypeDetector.DetectFileType(checkFileStream);
                checkFileStream.Close();
            }

            if (!File.Exists(fileName) || (fileType is not FileType.QuickTime && fileType is not FileType.Mp4))
            {
                await DeferAsync();
                downloadField.Value = "Failed.";

                uploadField.Value = $"Won't be uploaded, failed to fetch valid video file. Format: {fileType}";

                await mid.ModifyAsync(x => x.Embed = embed.Build());

                return;
            }

            downloadField.Value = "Done.";

            uploadField.Value = "In progress...";

            await mid.ModifyAsync(x => x.Embed = embed.Build());

            ulong size = (ulong)new FileInfo(fileName).Length;

            // check if the guild supports a file this large, otherwise fail.
            if (Context.Guild == null || Context.Guild.MaxUploadLimit > size)
            {
                try
                {
                    await RespondWithFileAsync(fileName);
                    _ = mid.DeleteAsync();
                }
                catch
                {
                    await DeferAsync();

                    uploadField.Value = "Failed.";

                    await mid.ModifyAsync(x => x.Embed = embed.Build());
                    return;
                }
            }
            else
            {
                _ = mid.DeleteAsync();
                uploadField.Value = "Failed.";
                embed.AddField("Video was too large.", $"Instead, have a [Direct link]({tweetUrl}).");
                await RespondAsync(embed: embed.Build());
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
           
            else
            {
                _ = (new EmojiDownloader.EmojiDownloader()).DownloadEmojis(Context);
            }
        }

        [SlashCommand("alarm", "Sets an alarm to ring after a given period of time (by default, in minutes).")]
        public async Task Alarm(int amount, string unit = "minute")
        {
            if (AlarmManager.CheckUserHasAlarm(Context.User))
            {
                await RespondAsync("You already have an alarm set! Only one alarm per user. You may also cancel your current alarm with $cancelalarm.", ephemeral: true);
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

            embed.Title = "Alarm set!";
            embed.Description = $"An alarm has been. You will be tagged <t:{((DateTimeOffset)(DateTime.Now + TimeSpan.FromMinutes(amount))).ToUnixTimeSeconds()}:R>";

            embed.AddField("Date and time of alert", $"{(DateTime.Now + TimeSpan.FromMinutes(amount)).ToUniversalTime()} (UTC)");

            await RespondAsync(embed: embed.Build());

            AlarmManager.CreateAlarm((DateTime.Now + TimeSpan.FromMinutes(amount)), await UserEngine.GetDBUser(Context.User), Context.Channel, amount);
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