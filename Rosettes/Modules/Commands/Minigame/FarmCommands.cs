﻿using Discord;
using Discord.Interactions;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Minigame.Farming;

namespace Rosettes.Modules.Commands.Minigame;

[Group("farm", "Farming system commands")]
public class FarmCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("view", "View your farm")]
    public async Task ViewFarm()
    {
        string isAllowed = await FarmEngine.CanUseFarmCommand(Context);
        if (isAllowed != "yes")
        {
            await RespondAsync(isAllowed, ephemeral: true);
            return;
        }

        await Farm.ShowFarm(Context.Interaction, Context.User);
    }

    [SlashCommand("fish", "Try to catch a fish")]
    public async Task CatchFish()
    {
        string isAllowed = await FarmEngine.CanUseFarmCommand(Context);
        if (isAllowed != "yes")
        {
            await RespondAsync(isAllowed, ephemeral: true);
            return;
        }
        await FarmEngine.CatchFishFunc(Context.Interaction, Context.User);
    }

    [SlashCommand("inventory", "Check your inventory")]
    public async Task FarmInventory()
    {
        string isAllowed = await FarmEngine.CanUseFarmCommand(Context);
        if (isAllowed != "yes")
        {
            await RespondAsync(isAllowed, ephemeral: true);
            return;
        }

        await FarmEngine.ShowInventoryFunc(Context.Interaction, Context.User);
    }

    [SlashCommand("shop", "See items available in the shop, or provide an option to buy.")]
    public async Task FarmShop()
    {
        string isAllowed = await FarmEngine.CanUseFarmCommand(Context);
        if (isAllowed != "yes")
        {
            await RespondAsync(isAllowed, ephemeral: true);
            return;
        }

        await FarmEngine.ShowShopFunc(Context.Interaction, Context.User);
    }

    [SlashCommand("give", "Give an item to another user.")]
    public async Task GiveItem(IUser user, string option = "none", int amount = 1)
    {
        string isAllowed = await FarmEngine.CanUseFarmCommand(Context);
        if (isAllowed != "yes")
        {
            await RespondAsync(isAllowed, ephemeral: true);
            return;
        }

        if (user == Context.User)
        {
            await RespondAsync("That's you! Give stuff to someone else.", ephemeral: true);
            return;
        }

        if (amount < 1)
        {
            await RespondAsync("You must give at least 1 of any item.", ephemeral: true);
            return;
        }

        string choice = option.ToLower();

        if (FarmEngine.IsValidGiveChoice(choice))
        {
            var dbUser = await UserEngine.GetDbUser(Context.User);
            var receiver = await UserEngine.GetDbUser(user);

            if (await FarmEngine.GetItem(dbUser, choice) < amount)
            {
                await RespondAsync($"You don't have {amount} {FarmEngine.GetItemName(choice)} to give.");
                return;
            }

            FarmEngine.ModifyItem(receiver, choice, +amount);

            FarmEngine.ModifyItem(dbUser, choice, -amount);

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
            embed.Title = "Item given.";
            embed.Description = $"Gave {amount} {FarmEngine.GetItemName(choice)} to {user.Mention}.";

            await RespondAsync(embed: embed.Build());
        }
        else
        {
            await RespondAsync("Valid things to give:\n >>> \"fish\",\r\n\"uncommonfish\",\r\n\"rarefish\",\r\n\"shrimp\",\r\n\"dabloons\",\r\n\"garbage\",\r\n\"tomato\",\r\n\"carrot\",\r\n\"potato\",\r\n\"seedbag\"", ephemeral: true);
        }
    }

    [SlashCommand("top", "Leaderbord by experience.")]
    public async Task FoodLeaderboard()
    {
        string isAllowed = await FarmEngine.CanUseFarmCommand(Context);
        if (isAllowed != "yes")
        {
            await RespondAsync(isAllowed, ephemeral: true);
            return;
        }
        var users = await UserEngine.GetAllUsersFromGuild(Context.Guild);

        if (users.Count == 0)
        {
            await RespondAsync("There was an error listing the top users in this guild, sorry.");
            return;
        }

        var topUsers = users.OrderByDescending(x => x.Exp).Take(10);

        string topList = "Top 10 by experience: ```";
        topList += "User                                 Level & Experience\n\n";

        foreach (var anUser in topUsers)
        {
            if (anUser.Exp <= 0) continue;

            var userName = await anUser.GetName();
            var space = 32 - userName.Length;
            userName += new string(' ', space);

            topList += $"{userName}|   Level {anUser.GetLevel()}; {anUser.Exp}xp\n";
        }

        topList += "```";

        await RespondAsync(topList);
    }
}