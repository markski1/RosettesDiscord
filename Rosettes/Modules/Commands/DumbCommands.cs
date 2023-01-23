using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Text.RegularExpressions;

namespace Rosettes.Modules.Commands
{
    public class DumbCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("makesweeper", "Make a minesweeper with a given emoji.")]
        public async Task MakeSweeper(string anEmoji, int difficulty = 2, string hideZeros = "false", string unspoilered = "false")
        {
            if (difficulty < 1 || difficulty > 3)
            {
                await RespondAsync("Difficulty must be from 1 to 3", ephemeral: true);
                return;
            }

            if (unspoilered != "false" && unspoilered != "true")
            {
                await RespondAsync("Valid values for unspoilered: 'false', 'true'", ephemeral: true);
                return;
            }

            if (hideZeros != "false" && hideZeros != "true")
            {
                await RespondAsync("Valid values for hideZeros: 'false', 'true'", ephemeral: true);
                return;
            }

            // set parameters for the board based on difficulty.
            int diffMod = difficulty;
            if (diffMod > 2) diffMod = 2;

            int gridWidth = 4 + (diffMod * 4);
            int gridHeight = 4 + (diffMod * 2);

            int mineCount = 8 + (difficulty * 6);
            
            string diffName = difficulty switch
            {
                1 => "Easy",
                2 => "Normal",
                _ => "Hard",
            };


            int[,] playingField = new int[gridWidth, gridHeight];

            Random rand = new();

            int i, j;

            // decide the position of every mine
            for (i = 0; i < mineCount; i++)
            {
                int x, y;
                // avoid repeats by breaking out of the 'loop' only if the chosen square has no other mines.
                while (true)
                {
                    x = rand.Next(gridWidth);
                    y = rand.Next(gridHeight);

                    if (playingField[x, y] == -1) continue;
                    break;
                }
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
                        if (unspoilered == "true")
                        {
                            board += $"{anEmoji}";
                        }
                        else
                        {
                            board += $"||{anEmoji}||";
                        }
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
                    // check to the right, if we're not at the very right.
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

                    // don't spoiler if that was requested.
                    if (unspoilered == "true")
                    {
                        board += $"{new Emoji($"{count}⃣")}";
                        continue;
                    }

                    // if the count is greater than 0, add a spoilered emoji with the count, otherwise just a zero - unless the user requested zeros are hidden too.
                    if (count > 0 || hideZeros == "true") board += $"||{new Emoji($"{count}⃣")}||";
                    else board += "0⃣";
                }
                // when done with a row, break line
                board += "\n";
            }

            EmbedBuilder embed = Global.MakeRosettesEmbed();

            if (unspoilered == "false")
            {
                embed.Title = $"{anEmoji}-Sweeper! - {diffName} difficulty.";
                embed.Description = $"In a {gridWidth}x{gridHeight} board; clear all the free squares, avoid the {mineCount} {anEmoji}'s!";
            } 
            else
            {
                embed.Title = $"Non-playable {anEmoji}-sweeper board generated.";
            }

            await RespondAsync(embed: embed.Build());
            // if the difficulty is 3, we send the first chunk of the board first.
            await Context.Channel.SendMessageAsync(board);
        }

        [SlashCommand("urban", "Returns an UrbanDictionary definition for the provided word.")]
        public async Task UrbanDefine(string query)
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command cannot be used in DM's.");
                return;
            }
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (!dbGuild.AllowsDumb())
            {
                await RespondAsync("Sorry, but the guild admins have disabled the use of this type of commands.");
                return;
            }

            if (query == "")
            {
                await RespondAsync($"Usage: `/urban <term>`");
                return;
            }

            if (!Regex.IsMatch(query, "^[a-zA-Z0-9 ]*$"))
            {
                await RespondAsync($"The term must only contain letters and numbers.");
                return;
            }
            
            string message;

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://mashape-community-urban-dictionary.p.rapidapi.com/define?term={query.ToLower()}"),
                Headers =
                    {
                        { "X-RapidAPI-Key", Settings.RapidAPIKey },
                        { "X-RapidAPI-Host", "mashape-community-urban-dictionary.p.rapidapi.com" },
                    },
            };

            try
            {
                using var response = await Global.HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsStringAsync();
                // obtain the definition from the API and parse it. Use "first" because it's all contained in a response object.
                var definitionList = JObject.Parse(data).First;
                if (definitionList is null)
                {
                    await RespondAsync("Failed to fetch definitions.");
                    return;
                }

                // "first" again because the definitions are inside a list (and as stated above, the list inside a response object. very stupid).
                definitionList = definitionList.First;

                // if the list is null, we have no results.
                if (definitionList is null)
                {
                    await RespondAsync("No definition found for that word.");
                    return;
                }

                List<dynamic> parsedDefinitionList = new();
                foreach (var aDefinition in definitionList)
                {
                    dynamic? temp = JsonConvert.DeserializeObject(aDefinition.ToString());
                    if (temp is null) continue;
                    parsedDefinitionList.Add(temp);
                }

                dynamic definition = parsedDefinitionList.OrderBy(def => def.thumbs_up - def.thumbs_down).Last();

                message =
                    $"Definition for: {query}" +
                    $"```" +
                    definition.definition +
                    $"```" +
                    $"**Upvotes**: {definition.thumbs_up} | **Downvotes**: {definition.thumbs_down}\n" +
                    $"**Permalink**: <{definition.permalink}>";

                await RespondAsync(message);
            }
            catch (Exception ex)
            {
                await RespondAsync("There was an error fetching the definition.", ephemeral: true);
                Global.GenerateErrorMessage("urbanDictionary", ex.Message);
            }
        }
    }
}