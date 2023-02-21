using Discord.Interactions;
using Rosettes.Modules.Engine;
using Rosettes.Core;
using Rosettes.Modules.Commands.Alarms;
using Discord;
using MetadataExtractor.Util;
using MetadataExtractor;
using Newtonsoft.Json;
using Discord.WebSocket;

namespace Rosettes.Modules.Commands
{
	public class UtilityCommands : InteractionModuleBase<SocketInteractionContext>
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
			embed.ThumbnailUrl = guildUser.GetDisplayAvatarUrl();

			embed.AddField("Level", $"Level {db_user.GetLevel()} ({db_user.Exp}xp)");
			embed.AddField("Joined Discord", $"<t:{guildUser.CreatedAt.ToUnixTimeSeconds()}:R>", true);
			if (guildUser.JoinedAt is DateTimeOffset guildJoin)
			{
				embed.AddField("Joined Server", $"<t:{guildJoin.ToUnixTimeSeconds()}:R>", true);
			}

            embed.AddField("Rosettes achievements", "None");

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
			embed.AddField("Owner", guild.Owner.Username + "#" + guild.Owner.Discriminator);
			embed.AddField("Stickers", guild.Stickers.Count, true);
			embed.AddField("Emoji", guild.Emotes.Count, true);
			if (guild.SplashUrl is not null)
			{
				embed.AddField("Splash image URL", $"<{guild.SplashUrl}>");
			}

			await RespondAsync(embed: embed.Build());
		}

		[MessageCommand("DL Twitter video")]
		public async Task TweetVideoMsg(IMessage message)
		{
			string url = Global.GrabURLFromText(message.Content);
			if (url != "0") await TweetVideo(url);
			else await RespondAsync("No URL found in this message.", ephemeral: true);
		}

		[SlashCommand("twtvid", "Get the video file of the specified tweet.")]
		public async Task TweetVideo(string tweetUrl)
		{
			string originalTweet = tweetUrl;

			int tldEnd = tweetUrl.IndexOf("twitter.com");

			if (tldEnd == -1)
			{
				await RespondAsync("That's not a valid tweet URL.", ephemeral: true);
				return;
			}

			// position this number in the location where "twitter.com" ends in the URL.
			tldEnd += 11;

			// remove anything before twitter.com (should also cover links from fxtwitter, sxtwitter etc)
			tweetUrl = tweetUrl[tldEnd..tweetUrl.Length];

			// form a link straight to FxTwitter's direct media backend.
			tweetUrl = $"https://d.fxtwitter.com{tweetUrl}";
			
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

			// retrieve the video's format.
			using var checkFileStream = new FileStream(fileName, FileMode.Open);
			FileType fileType = FileType.Unknown;
			if (checkFileStream is not null)
			{
				fileType = FileTypeDetector.DetectFileType(checkFileStream);
				checkFileStream.Close();
			}

			// ensure the file was downloaded correctly and is either MPEG4 or QuickTime encoded.
			if (!System.IO.File.Exists(fileName) || (fileType is not FileType.QuickTime && fileType is not FileType.Mp4))
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
				uploadField.Value = "Failed.";
				embed.AddField("Video was too large.", $"Instead, have a [Direct link]({tweetUrl}).");
				await RespondAsync(embed: embed.Build());
				_ = mid.DeleteAsync();
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

			await RespondAsync("Your feedback has been sent. All feedback is read and taken into account. If a suggestion you sent is implemented or an issue you pointed out is resolved, you might receive a DM from Rosettes letting you know of this.\n \n If you don't allow DM's from bots, you may not receive anything or get a friend request from Markski#7243 depending on severity.", ephemeral: true);
		}

		[MessageCommand("Reverse GIF")]
		public async Task ReverseGIFMessageCMD(IMessage message)
		{
			await RespondAsync("Reversing, please wait...");

			string getUrl = Global.GrabURLFromText(message.Content);

			// first try to find a gif attached
			if (message.Attachments.Any())
			{
				string fileType = message.Attachments.First().ContentType.ToLower();
				if (fileType.Contains("/gif"))
				{
					getUrl = message.Attachments.First().Url;
				}
			}
			// else, check if it's a tenor url. If that's the case, we need to get a direct link to the gif through the API.
			else if (getUrl.Contains("tenor.com"))
			{
				getUrl = await GetDirectTenorURL(getUrl);
			}

			// if we got a url to fetch, go for it.
			if (getUrl != "0")
			{
				await ReverseGIF(getUrl);
			}
			else
			{
				// else, last attempt to get something: try to fetch it out of an emote.
				try
				{
					Emote emote = Emote.Parse(message.Content);
					await ReverseGIF(emote.Url);
				}
				catch
				{
					// welp, we found nothing to work with.
					await ModifyOriginalResponseAsync(x => x.Content = "No images or animated emotes found in this message.");
				}
			}         
		}

		[SlashCommand("reversegif", "[experimental] Reverse the gif in the provided URL.")]
		public async Task ReverseGIFSlashCMD(string gifUrl)
		{
			await RespondAsync("Reversing, please wait...");

			string getUrl = Global.GrabURLFromText(gifUrl);

			// check if it's a tenor url. If that's the case, we need to get a direct link to the gif through the API.
			if (getUrl.Contains("tenor.com"))
			{
				getUrl = await GetDirectTenorURL(getUrl);
			}

			if (getUrl != "0")
			{
				await ReverseGIF(getUrl);
			}
			else
			{
				await ModifyOriginalResponseAsync(x => x.Content = "Sorry, there was an error fetching the gif.");
			}
		}

		[SlashCommand("tenorparse", "Get direct media URLs off a tenor link")]

		public async Task TenorParse(string url)
		{
			if (!url.Contains("/tenor.com"))
			{
				await RespondAsync("Not a valid tenor url");
				return;
			}

			string tenorUrl = await GetDirectTenorURL(url);

			if (url == "0")
			{
				await RespondAsync("Not a valid tenor url");
				return;
			}

			string tenorWebmUrl = tenorUrl.Replace(".gif", ".webm");

			await RespondAsync($"Direct GIF url: `{tenorUrl}`\nDirect WEBM url: `{tenorWebmUrl}`");
		}

		public static async Task<string> GetDirectTenorURL(string tenorUrl)
		{
			// check it isn't a direct link already
			if ((tenorUrl.Contains("/media.tenor") || tenorUrl.Contains("/c.tenor")) && tenorUrl.Contains(".gif"))
			{
				return tenorUrl;
			}

			int ends = tenorUrl.Length;

			// a valid tenor url will end with a number.
			if (Char.IsNumber(tenorUrl[ends - 1]))
			{
				// the number in question is the post ID, which we must extract out of the url
				// to do this, check where the number begins by working our way back until there's no more numbers.
				int start = ends - 1;
				try {
					while (char.IsNumber(tenorUrl[start]))
					{
						start--;
					}
				}
				catch
				{
					return "0";
				}
				start++;
				// now that we know where the number begins and ends, extract it and use it to ask the API for the media url.
				string id = tenorUrl[start..ends];
				string requestUrl = $"https://tenor.googleapis.com/v2/posts?key={Settings.TenorKey}&ids={id}";
				var data = await Global.HttpClient.GetStringAsync(requestUrl);

				// deserialize it into a dynamic object.
				var DeserialziedObject = JsonConvert.DeserializeObject(data);
				if (DeserialziedObject == null)
				{
					return "0";
				}
				dynamic results = ((dynamic)DeserialziedObject).results;

				try
				{
					foreach (var result in results)
					{
						// try to return a 'mediumgif' element, we can't really check that it's in the dynamic object though
						try
						{
							return result.media_formats.mediumgif.url;
						}
						// if there's no mediumgif we'll face an exception, in which case just return the base gif
						catch
						{
							return result.media_formats.gif.url;
						}
					}
				}
				catch { return "0"; }
			}
			return "0";
		}

		public async Task ReverseGIF(string url)
		{
			Random rand = new();
			string randomValue = $"{rand.Next(100)}";
			if (!System.IO.Directory.Exists("/var/www/html/brickthrow/reverseCache/"))
			{
				System.IO.Directory.CreateDirectory("/var/www/html/brickthrow/reverseCache/");
			}
			if (!System.IO.Directory.Exists("/var/www/html/brickthrow/generated/"))
			{
				System.IO.Directory.CreateDirectory("/var/www/html/brickthrow/generated/");
			}
			string fileName = $"/var/www/html/brickthrow/reverseCache/{randomValue}.gif";
			if (System.IO.File.Exists(fileName))
			{
				System.IO.File.Delete(fileName);
			}

			using (Stream stream = await Global.HttpClient.GetStreamAsync(url))
			{
				using var fileStream = new FileStream(fileName, FileMode.Create);
				var downloadTask = stream.CopyToAsync(fileStream);
				int quarterSecondCount = 0;
				while (!downloadTask.IsCompleted)
				{
					await Task.Delay(250);
					quarterSecondCount++;
					if (quarterSecondCount >= 20) // if the download takes more than 5 seconds it's probably not a very honest url
					{
						await FollowupAsync("Cancelled: GIF download took too long.");
						// Can't dipose an unfinished task, but upon testing, the GC consistently takes care of this
						return;
					}
				}
			}

			fileName = $"/var/www/html/brickthrow/generated/{randomValue}.gif";
			if (System.IO.File.Exists(fileName))
			{
				System.IO.File.Delete(fileName);
			}
			using (var stream = await Global.HttpClient.GetStreamAsync($"https://snep.markski.ar/brickthrow/reverse.php?imageNum={randomValue}"))
			{
				using var fileStream = new FileStream(fileName, FileMode.Create);
				await stream.CopyToAsync(fileStream);
			}
			ulong size = (ulong)new FileInfo(fileName).Length;

			if (size > 1024)
			{
				await FollowupWithFileAsync(fileName);
			}
			else
			{
				await FollowupAsync("The provided file was not a gif.", ephemeral: true);
			}
			System.IO.File.Delete(fileName);
		}
	}
}