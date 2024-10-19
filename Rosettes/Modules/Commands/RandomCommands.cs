using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PokeApiNet;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Guild;

namespace Rosettes.Modules.Commands;

public class RandomCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("roll", "Roll dice. Use a number, or standard dice notation (i.e. 1d6)")]
    public async Task Dice(string options)
    {
        var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
        var dbUser = await UserEngine.GetDBUser(Context.User);

        if (!dbGuild.AllowsRandom())
        {
            await RespondAsync("Sorry, but the guild admins have disabled the use of this type of commands.", ephemeral: true);
            return;
        }

        int diceCount;
        int diceFaces;

        try
        {
            string[] parts = options.Split('d');

            if (parts.Length > 1)
            {
                diceFaces = int.Parse(parts[1]);
                diceCount = int.Parse(parts[0]);
            }
            else
            {
                diceFaces = int.Parse(parts[0]);
                diceCount = 1;
            }
        }
        catch
        {
            await RespondAsync("Please enter a valid value.", ephemeral: true);
            return;
        }
        

        if (diceFaces < 2 || diceCount < 1)
        {
            await RespondAsync("Dice must have at least 1 face, and you must roll at least one die.", ephemeral: true);
            return;
        }
        else if (diceFaces > 10000000 || diceCount > 50)
        {
            await RespondAsync("Dice may not have more than 10 million faces, and you may roll no more than 50 dice at once.", ephemeral: true);
            return;
        }
        
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.WithTitle($"Rolled {diceCount}d{diceFaces}.");

        if (diceCount == 1)
        {
            embed.WithDescription($"Result: {Global.Randomize(diceFaces) + 1}");
        }
        else
        {
            List<int> results = [];

            for (int i = 0; i < diceCount; i++) results.Add(Global.Randomize(diceFaces) + 1);

            embed.AddField("Roll results", String.Join(", ", results));
            embed.AddField("Total", $"{results.Sum()}; average value {results.Sum() / diceCount}.");
        }

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("coin", "Throw a coin! You may provide two custom faces.")]
    public async Task CoinCommand([Summary("face-1", "By default, Tails")] string face1 = "Tails", [Summary("face-2", "By default, Heads")] string face2 = "Face")
    {
        var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
        if (!dbGuild.AllowsRandom())
        {
            await RespondAsync("Sorry, but the guild admins have disabled the use of this type of commands.", ephemeral: true);
            return;
        }

        string[] coinSides = [face1, face2];

        var dbUser = await UserEngine.GetDBUser(Context.User);
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
        embed.Title = "A coin is thrown in the air...";
        embed.Description = $"{face1} in one side, {face2} in the other.";

        embed.AddField("Result:", $"{coinSides[Global.Randomize(0, 2)]}");

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("checkem", "Want to gamble something on dubs, trips, maybe even quads? Check'Em!")]
    public async Task CheckEm()
    {
        int number = Global.Randomize(99999999) + 1;
        // kind of a hacky way to ensure the number is 8 digits long. This is just a memey random number thing, so it doesn't matter.
        if (number < 10000000)
        {
            number += 10000000;
        }

        string displayName;

        if (Context.User is SocketGuildUser guildUser)
        {
            displayName = guildUser.DisplayName;
        }
        else
        {
            displayName = Context.User.Username;
        }

        await RespondAsync($"[{displayName}] Check'Em! : **{number}**");
    }
}