using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Guild;
using System.Data;

namespace Rosettes.Modules.Commands;

public class RandomCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("dice", "Roll dice.")]
    public async Task Dice(int dice_faces, int dice_count = 1)
    {
        var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
        var dbUser = await UserEngine.GetDBUser(Context.User);

        if (dbGuild is not null)
        {
            if (!dbGuild.AllowsRandom())
            {
                await RespondAsync("Sorry, but the guild admins have disabled the use of this type of commands.", ephemeral: true);
                return;
            }
        }

        if (dice_faces < 2 || dice_count < 1)
        {
            await RespondAsync("Dice must have at least 1 face, and you must roll at least one dice.", ephemeral: true);
            return;
        }
        else if (dice_faces > 1000000 || dice_count > 10)
        {
            await RespondAsync("Dice may not have more than 1 million faces, and you may roll no more than 10 dice at once.", ephemeral: true);
            return;
        }
        else
        {
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.WithTitle($"Rolled {dice_count} dice.");
            embed.WithDescription($"With {dice_faces} faces each.");

            string resultText = "";
            int diceTotal = 0;
            for (int i = 0; i < dice_count; i++)
            {
                int diceResult = Global.Randomize(dice_faces) + 1;
                resultText += $"**Die {i + 1}:** {diceResult} \n";
                diceTotal += diceResult;
            }

            embed.AddField("Roll results: ", resultText);

            embed.AddField("Total:", $"{diceTotal}, with each die scoring {diceTotal / dice_count} in average.");

            await RespondAsync(embed: embed.Build());
        }
    }

    [SlashCommand("coin", "Throw a coin! You may provide two custom faces.")]
    public async Task CoinCommand([Summary("face-1", "By default, Tails")] string face1 = "Tails", [Summary("face-2", "By default, Heads")] string face2 = "Face")
    {
        var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
        if (dbGuild is not null)
        {
            if (!dbGuild.AllowsRandom())
            {
                await RespondAsync("Sorry, but the guild admins have disabled the use of this type of commands.", ephemeral: true);
                return;
            }
        }

        string[] coinSides = { face1, face2 };

        var dbUser = await UserEngine.GetDBUser(Context.User);
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
        embed.Title = "A coin is thrown in the air...";
        embed.Description = $"{face1} in one side, {face2} in the other.";

        embed.AddField("Result:", $"{coinSides[Global.Randomize(0, 2)]}");

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("checkem", "Want to gamble something on dubs, trips, maybe even quads? Check'Em!")]
    public async Task CheckEm([Summary("image", "Return a relevant checkem image. By default, false.")] string image = "false")
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command cannot be used in DM's. Instead use https://snep.markski.ar/checkem");
            return;
        }

        if (image != "false" && image != "true")
        {
            await RespondAsync("the 'image' parameter may only be 'true' or 'false'(default)", ephemeral: true);
            return;
        }

        int number = Global.Randomize(99999999) + 1;
        // kind of a hacky way to ensure the number is 8 digits long. This is just a memey random number thing so it doesn't matter.
        if (number < 10000000)
        {
            number += 10000000;
        }

        string displayName;
        SocketGuildUser? GuildUser = Context.User as SocketGuildUser;
        if (GuildUser is not null && GuildUser.Nickname is not null)
        {
            displayName = GuildUser.DisplayName;
        }
        else
        {
            displayName = Context.User.Username;
        }

        await RespondAsync($"[{displayName}] Check'Em! : **{number}**");

        if (image == "true")
        {
            var File = Directory.GetFiles("/var/www/html/checkem/").OrderBy(x => Global.Randomizer.Next()).Take(1);
            await ReplyAsync(File.First().Replace("/var/www/html/", "https://snep.markski.ar/"));
        }
    }
}