using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Rosettes.Database;
using Rosettes.Managers;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Farming;
using System.Diagnostics;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Rosettes.Core
{
    public static class Global
	{
		public static readonly RosettesMain RosettesMain = new();
		public static readonly HttpClient HttpClient = new();

		public static async void SendMessage(ulong id, string message)
		{
			try
			{
				var user = await UserEngine.GetUserReferenceByID(id);
				if (user is SocketUser socketUser)
					await socketUser.SendMessageAsync($"ADMIN MESSAGE:```{message}```");
			}
			catch (Exception ex)
			{
				GenerateErrorMessage("global", $"failed to deliver admin message to user {id} \n----\n {ex}");
			}
		}

		

		public static void WriteToFs(ref FileStream fs, string text)
		{
			Byte[] textAsBytes = new UTF8Encoding(true).GetBytes(text);
			fs.Write(textAsBytes, 0, textAsBytes.Length);
		}

		public static async Task<EmbedBuilder> MakeRosettesEmbed(User? dbUser = null)
		{
			EmbedBuilder embed = new()
			{
				Color = Color.DarkPurple
			};

			if (dbUser is not null)
			{
				IUser? author;
				author = await dbUser.GetDiscordReference();

				if (author is not null)
				{
					SocketGuildUser? GuildUser = author as SocketGuildUser;
					EmbedAuthorBuilder authorEmbed = new();
					embed.Author = authorEmbed;
					if (GuildUser is not null && GuildUser.Nickname is not null)
					{
						authorEmbed.Name = GuildUser.DisplayName;
						if (GuildUser.GetDisplayAvatarUrl() is not null)
						{
							authorEmbed.IconUrl = GuildUser.GetDisplayAvatarUrl();
						}
					}
					else
					{
						authorEmbed.Name = author.Username;
						if (author.GetAvatarUrl() is not null)
						{
							authorEmbed.IconUrl = author.GetAvatarUrl();
						}
					}
					if (dbUser.MainPet > 0)
					{
						authorEmbed.Name += $" | with {FarmEngine.PetEmojis(dbUser.MainPet)} pet";
					}
					authorEmbed.Name += $" [lv {dbUser.GetLevel()}]";
				}
			}
			
			embed.ThumbnailUrl = "https://markski.ar/images/trans.png";

			return embed;
		}

		public static void GenerateErrorMessage(string source, string error)
		{
			// generate the error string
			string _error = $"There was an error at \"{source}\".\n```{error}```\n";

			// send it to error channel
			var client = ServiceManager.GetService<DiscordSocketClient>();
			if (client.GetChannel(984608927775854594) is not ITextChannel errorChannel) return;

			if (_error.Length > 1999)
			{
				_error = _error[..1900];
				_error += "```(truncated)";
			}

			errorChannel.SendMessageAsync(_error);

			// and log it to a file
			_error = $"{DateTime.UtcNow} | There was an error at \"{source}\".\n{error}\n\n";
			try
			{
				var fileStream = new FileStream("./errors.log", FileMode.OpenOrCreate, FileAccess.ReadWrite);
				WriteToFs(ref fileStream, _error);
				fileStream.Close();
			}
			catch
			{
				// meh, just don't stop
			}
		}

		public static void GenerateNotification(string message)
		{
			// send it to #impawtant-data
			var client = ServiceManager.GetService<DiscordSocketClient>();
			if (client.GetChannel(984608927775854594) is not ITextChannel impawtantChannel) return;

			impawtantChannel.SendMessageAsync(message);
		}

		public static int CurrentUnix()
		{
			return (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
		}

		public static string GrabURLFromText(string text)
		{
			// try to grab the first URL from the received text.
			// Start by finding the first instance of http, and end as soon as we find a space or a control character.
			// return "0" if we can't find a url.
			int begin;
			begin = text.IndexOf("https:/");
			if (begin == -1)
			{
				begin = text.IndexOf("http:/");
				if (begin == -1) return "0";
			}
			int end = -1;
			for (int i = begin; i < end; i++)
			{
				if (text[i] == ' ' || char.IsControl(text[i]))
				{
					end = i;
					break;
				}
			}
			if (end == -1)
			{
				end = text.Length;
			}
			string url = text[begin..end];
			//remove anti-embed artifacts
			url = url.Replace(">", string.Empty);
			return url;
		}

		public static bool CheckSnep(ulong id)
		{
			if (id == 93115098461110272)
			{
				return true;
			}
			return false;
		}

		public static Task<int> RunBash(this string cmd)
		{
			var source = new TaskCompletionSource<int>();
			var escapedArgs = cmd.Replace("\"", "\\\"");
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "bash",
					Arguments = $"-c \"{escapedArgs}\"",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				},
				EnableRaisingEvents = true
			};
			process.Exited += (sender, args) =>
			{
				if (process.ExitCode == 0)
				{
					source.SetResult(0);
				}
				else
				{
					source.SetException(new Exception($"Command `{cmd}` failed with exit code `{process.ExitCode}`"));
				}

				process.Dispose();
			};

			try
			{
				process.Start();
			}
			catch (Exception e)
			{
				source.SetException(e);
			}

			return source.Task;
		}

		public static bool CheckIsEmoteOrEmoji(string anEmoji)
		{
			try
			{
				Emote.Parse(anEmoji);
				return true;
			}
			catch
			{
				try
				{
					Emoji.Parse(anEmoji);
					return true;
				}
				catch
				{
					return false;
				}
			}
		}

		public class MessageDeleter
		{
			private readonly System.Timers.Timer Timer = new();
			private readonly Discord.Rest.RestUserMessage message;

			public MessageDeleter(Discord.Rest.RestUserMessage _message, int seconds)
			{
				Timer.Elapsed += DeleteMessage;
				Timer.Interval = seconds * 1000;
				message = _message;
				Timer.Enabled = true;
			}

			public void DeleteMessage(Object? source, System.Timers.ElapsedEventArgs e)
			{
				message.DeleteAsync();
				Timer.Stop();
				Timer.Dispose();
			}
		}
	}
}