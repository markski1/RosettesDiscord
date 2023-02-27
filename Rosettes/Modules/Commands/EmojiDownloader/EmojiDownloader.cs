using Discord;
using Discord.Interactions;
using Rosettes.Core;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Rosettes.Modules.Commands.EmojiDownloader
{
	public class EmojiDownloader
	{
		private IReadOnlyCollection<GuildEmote>? EmoteCollection;

		public async Task DownloadEmojis(SocketInteractionContext ServerContext)
		{
			EmoteCollection = await ServerContext.Guild.GetEmotesAsync();
			string ServerName = ServerContext.Guild.Name;
			int emoteAmount = EmoteCollection.Count;
			int progress = 0;

			EmbedBuilder embed = await Global.MakeRosettesEmbed();

			embed.Title = "Exporting emoji.";

			EmbedFieldBuilder statusField = new() { Name = "Status" };

			embed.AddField(statusField);

			if (emoteAmount < 1)
			{
				await ServerContext.Interaction.RespondAsync("There are no custom emoji in this server, or I failed to retrieve them for some reason.");
				return;
			}
			else
			{
				statusField.Value = $"Progress: `0/{emoteAmount}`";
				await ServerContext.Interaction.RespondAsync(embed: embed.Build());
			}

			string fileName = "";
			
			// clean up servername
			string serverName = ServerContext.Guild.Name.Replace(" ", "");
			Regex rgx = new("[^a-zA-Z0-9 -]");
			serverName = rgx.Replace(serverName, "");

			// ensure the folders to store the emoji exist.
			if (!Directory.Exists("./temp/"))
			{
				Directory.CreateDirectory("./temp/");
			}
			if (!Directory.Exists($"./temp/{serverName}/"))
			{
				Directory.CreateDirectory($"./temp/{serverName}/");
			}
			// download every emoji into this folder.
			foreach (GuildEmote emote in EmoteCollection)
			{
				using (var stream = await Global.HttpClient.GetStreamAsync(emote.Url))
				{
					if (emote.Animated)
					{
						fileName = $"./temp/{serverName}/{emote.Name}.gif";
					}
					else
					{
						fileName = $"./temp/{serverName}/{emote.Name}.png";
					}
					using var fileStream = new FileStream(fileName, FileMode.Create);
					await stream.CopyToAsync(fileStream);
				}
				progress++;
				// update the message with the current progress, every 3rd emoji.
				if (progress % 3 == 0)
				{
					statusField.Value = $"Progress: `{progress}/{emoteAmount}`";
					try
					{
                        await ServerContext.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
                    }
					catch
					{
						_ = Task.Run(async () => { await ServerContext.User.SendMessageAsync("I don't have permission to send or edit messasges in that channel, can't complete emoji export."); } );
						return;
					}
				}
			}
			// done, update message.
			statusField.Value = $"Progress: `Done exporting. Compressing and uploading, please wait...`";
			await ServerContext.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
			try
			{
				// zip up the file.
				string zipPath = $"./temp/{serverName}.zip";
				if (File.Exists(zipPath))
				{
					File.Delete(zipPath);
				}
				ZipFile.CreateFromDirectory($"./temp/{serverName}", zipPath);

				// move the zip file to the webserver.
				if (File.Exists($"/var/www/html/{serverName}.zip"))
				{
					File.Delete($"/var/www/html/{serverName}.zip");
				}
				File.Move(zipPath, $"/var/www/html/{serverName}.zip");

				statusField.Value = $"Progress: `Done exporting. Done uploading.`";
				embed.AddField("Download link", $"<https://snep.markski.ar/{serverName}.zip>");
				await ServerContext.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
			}
			catch (Exception ex)
			{
				statusField.Value = $"Progress: `Failed. Sorry, the error has been reported.`";
				Global.GenerateErrorMessage("emojiExporter", $"{ex}");
				await ServerContext.Interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
			}
		}
	}
}
