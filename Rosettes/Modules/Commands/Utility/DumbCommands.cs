using Discord;
using Discord.Interactions;
using Rosettes.Core;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using Point = SixLabors.ImageSharp.Point;
using Size = SixLabors.ImageSharp.Size;

namespace Rosettes.Modules.Commands.Utility;

public class DumbCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("makesweeper", "Make a minesweeper with a given emoji.")]
    public async Task MakeSweeper(
        [Summary("emoji", "Emoji to be used as a mine.")] string anEmoji, 
        [Summary("difficulty", "Difficulty of the resulting minesweeper, 1-3.")] 
        [Choice("Easy", 1)]
        [Choice("Medium", 2)]
        [Choice("Hard", 3)]
        int difficulty = 2
    )
    {
        if (difficulty is < 1 or > 3)
        {
            await RespondAsync("Difficulty must be from 1 to 3", ephemeral: true);
            return;
        }

        try
        {
            Emote.Parse(anEmoji);
        }
        catch
        {
            try
            {
                Emoji.Parse(anEmoji);
            }
            catch
            {
                await RespondAsync("Invalid emoji entered", ephemeral: true);
            }
            return;
        }

        // set parameters for the board based on difficulty.
        int diffMod = difficulty;
        if (diffMod > 2) diffMod = 2;

        int gridWidth = 4 + diffMod * 4;
        int gridHeight = 4 + diffMod * 2;

        int mineCount = 8 + difficulty * 6;

        string diffName = difficulty switch
        {
            1 => "Easy",
            2 => "Normal",
            _ => "Hard"
        };


        int[,] playingField = new int[gridWidth, gridHeight];

        int i, j;

        // decide the position of every mine
        for (i = 0; i < mineCount; i++)
        {
            int x, y;

            // find X and Y with no repeats.
            do
            {
                x = Global.Randomize(gridWidth);
                y = Global.Randomize(gridHeight);
            }
            while (playingField[x, y] == -1);

            // set -1 where the mine is
            playingField[x, y] = -1;
        }

        string board = "";

        // Go through every square in the board.
        for (j = 0; j < gridHeight; j++)
        {
            for (i = 0; i < gridWidth; i++)
            {
                // If the space contains a "mine", fill in the emoji.
                if (playingField[i, j] == -1)
                {
                    board += $"||{anEmoji}||";
                    continue;
                }

                // if not a mine, count nearby mines
                int count = 0;

                // check to the left if we're not at the very left.
                if (i != 0)
                {
                    if (playingField[i - 1, j] == -1)
                        count++;

                    if (j != 0 && playingField[i - 1, j - 1] == -1)
                        count++;

                    if (j != gridHeight - 1 && playingField[i - 1, j + 1] == -1)
                        count++;
                }
                // check to the right if we're not at the very right.
                if (i != gridWidth - 1)
                {
                    if (playingField[i + 1, j] == -1)
                        count++;

                    if (j != 0 && playingField[i + 1, j - 1] == -1)
                        count++;

                    if (j != gridHeight - 1 && playingField[i + 1, j + 1] == -1)
                        count++;
                }

                // check up and down, if anything.
                if (j != 0 && playingField[i, j - 1] == -1)
                    count++;
                if (j != gridHeight - 1 && playingField[i, j + 1] == -1)
                    count++;

                playingField[i, j] = count;

                // if the count is greater than 0, add a spoilered emoji with the count, otherwise just a zero - unless the user requested zeros are hidden too.
                if (count > 0) board += $"||{new Emoji($"{count}⃣")}||";
                else board += "0⃣";
            }
            // when done with a row, break line
            board += "\n";
        }

        EmbedBuilder embed = await Global.MakeRosettesEmbed();

        embed.Title = $"{anEmoji}-Sweeper! - {diffName} difficulty.";
        embed.Description = $"In a {gridWidth}x{gridHeight} board; clear all the free squares, avoid the {mineCount} {anEmoji}'s.";

        await RespondAsync(embed: embed.Build());
        // if the difficulty is 3, we send the first chunk of the board first.
        await Context.Channel.SendMessageAsync(board);
    }

    [SlashCommand("sus", "Generate a crewmate with emojis of your choice")]
    public async Task SusEmoji(string anEmoji, string glassEmoji = "🟦")
    {
        bool check = Global.CheckIsEmoteOrEmoji(anEmoji) && Global.CheckIsEmoteOrEmoji(glassEmoji);

        if (!check)
        {
            await RespondAsync("One of the entered parameters is not a valid emojte or emoji", ephemeral: true);
            return;
        }

        await RespondAsync($"▪️▪️▪️▪️▪️▪️▪️⬛⬛⬛\n▪️▪️▪️▪️▪️⬛⬛{anEmoji}{anEmoji}⬛⬛\n▪️▪️▪️▪️⬛{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}⬛\n▪️▪️▪️⬛{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}⬛\n▪️▪️▪️⬛{anEmoji}{anEmoji}{anEmoji}⬛⬛⬛⬛⬛⬛\n▪️▪️⬛⬛{anEmoji}{anEmoji}⬛{glassEmoji}{glassEmoji}{glassEmoji}{glassEmoji}{glassEmoji}{glassEmoji}⬛\n▪️⬛{anEmoji}⬛{anEmoji}{anEmoji}⬛{glassEmoji}{glassEmoji}{glassEmoji}{glassEmoji}{glassEmoji}{glassEmoji}⬛\n⬛{anEmoji}{anEmoji}⬛{anEmoji}{anEmoji}⬛{glassEmoji}{glassEmoji}{glassEmoji}{glassEmoji}{glassEmoji}{glassEmoji}⬛\n⬛{anEmoji}{anEmoji}⬛{anEmoji}{anEmoji}{anEmoji}⬛⬛⬛⬛⬛⬛");

        await ReplyAsync($"⬛{anEmoji}{anEmoji}⬛{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}⬛\n⬛{anEmoji}{anEmoji}⬛{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}⬛\n▪️⬛{anEmoji}⬛{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}{anEmoji}⬛\n▪️▪️⬛⬛{anEmoji}{anEmoji}⬛⬛⬛⬛⬛{anEmoji}⬛\n▪️▪️▪️⬛{anEmoji}{anEmoji}⬛▪️▪️▪️⬛{anEmoji}⬛\n▪️▪️▪️⬛{anEmoji}{anEmoji}⬛▪️▪️▪️⬛{anEmoji}⬛\n▪️▪️▪️⬛{anEmoji}{anEmoji}⬛▪️▪️▪️⬛{anEmoji}⬛\n▪️▪️▪️⬛⬛⬛⬛▪️▪️▪️⬛⬛⬛");
    }
    
        [SlashCommand("bubble", "Add a bubble overlay on an image.")]
    public async Task Bubble([Summary("image", "Attached image to be used.")] IAttachment image)
    {
        if (image.ContentType == null || !image.ContentType.StartsWith("image/"))
        {
            await RespondAsync("Please attach a valid image file.", ephemeral: true);
            return;
        }

        await DeferAsync();
        try
        {
            var httpClient = new HttpClient();
            var baseImage = SixLabors.ImageSharp.Image.Load(await httpClient.GetByteArrayAsync(image.Url););
            
            string bubblePath = Path.Combine("Assets", "speech-bubble.png");
            var bubbleOverlay = await SixLabors.ImageSharp.Image.LoadAsync(bubblePath);
            
            bubbleOverlay.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(baseImage.Width, baseImage.Height),
                Mode = ResizeMode.Max
            }));
            
            baseImage.Mutate(x => x.DrawImage(bubbleOverlay, new Point(0, 0), 1f));
            
            using var outputStream = new MemoryStream();
            await baseImage.SaveAsync(outputStream, new PngEncoder());
            outputStream.Position = 0;
            
            EmbedBuilder embed = await Global.MakeRosettesEmbed();
            embed.Title = "Speech Bubble";
            embed.ImageUrl = "attachment://bubble-result.png";
            
            await FollowupWithFileAsync(
                fileStream: outputStream,
                fileName: "bubble-result.png",
                embed: embed.Build()
            );
        }
        catch (Exception ex)
        {
            await FollowupAsync($"An error occurred while processing the image: {ex.Message}", ephemeral: true);
        }

    }
}