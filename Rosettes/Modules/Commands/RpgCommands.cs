using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.VisualBasic;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
    [Group("rpg", "RPG system commands")]
    public class RpgCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("fish", "Try to catch a fish")]
        public async Task CatchFish()
        {
            string isAllowed = await RpgEngine.CanuseRPGCommand(Context);
            if (isAllowed != "yes")
            {
                await RespondAsync(isAllowed, ephemeral: true);
                return;
            }
            await RpgEngine.CatchFishFunc(Context.Interaction, Context.User);
        }

        [SlashCommand("inventory", "Check your inventory")]
        public async Task RpgInventory()
        {
            string isAllowed = await RpgEngine.CanuseRPGCommand(Context);
            if (isAllowed != "yes")
            {
                await RespondAsync(isAllowed, ephemeral: true);
                return;
            }

            await RpgEngine.ShowInventoryFunc(Context.Interaction, Context.User);
        }

        [SlashCommand("farm", "View your farm")]
        public async Task RpgFarm()
        {
            string isAllowed = await RpgEngine.CanuseRPGCommand(Context);
            if (isAllowed != "yes")
            {
                await RespondAsync(isAllowed, ephemeral: true);
                return;
            }

            await RpgEngine.ShowFarm(Context.Interaction, Context.User);
        }

        [SlashCommand("shop", "See items available in the shop, or provide an option to buy.")]
        public async Task RpgShop()
        {
            string isAllowed = await RpgEngine.CanuseRPGCommand(Context);
            if (isAllowed != "yes")
            {
                await RespondAsync(isAllowed, ephemeral: true);
                return;
            }
            
            await RpgEngine.ShowShopFunc(Context.Interaction, Context.User);
        }

        [SlashCommand("give", "Give an item to another user.")]
        public async Task GiveItem(IUser user, string option = "none")
        {
            await RespondAsync("As part of a future update, giving items is currently disabled.");
            /*
            string isAllowed = await RpgEngine.CanuseRPGCommand(Context);
            if (isAllowed != "yes")
            {
                await RespondAsync(isAllowed, ephemeral: true);
                return;
            }

            string choice = option.ToLower();

            if (RpgEngine.IsValidGiveChoice(choice))
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                var receiver = await UserEngine.GetDBUser(user);

                if (await RpgEngine.GetItem(dbUser, choice) < 1)
                {
                    await RespondAsync($"You don't have any {RpgEngine.GetItemName(choice)} to give.");
                    return;
                }

                RpgEngine.ModifyItem(receiver, choice, +1);

                RpgEngine.ModifyItem(dbUser, choice, -1);

                EmbedBuilder embed = await Global.MakeRosettesEmbed();
                embed.Title = "Item given.";

                embed.AddField(Context.User.Mention, $"Given {RpgEngine.GetItemName(choice)} to {user.Mention}!");

                await RespondAsync(embed: embed.Build());
            }
            else
            {
                await RespondAsync("Valid things to give: sushi", ephemeral: true);
                return;
            }
            */
        }
        
        [SlashCommand("top", "Leaderbord by experience.")]
        public async Task FoodLeaderboard()
        {
            string isAllowed = await RpgEngine.CanuseRPGCommand(Context);
            if (isAllowed != "yes")
            {
                await RespondAsync(isAllowed, ephemeral: true);
                return;
            }
            var users = await UserEngine.GetAllUsersFromGuild(Context.Guild);

            if (users is null)
            {
                await RespondAsync("There was an error listing the top users in this guild, sorry.");
                return;
            }

            var topUsers = users.OrderByDescending(x => x.Exp).Take(10);

            string topList = "Top 10 by experience: ```";
            topList += $"User                                 Level & Experience\n\n";
            var spaceStr = "";
            var space = 30;

            foreach (var anUser in topUsers)
            {
                spaceStr = "";
                space = 32 - (await anUser.GetName(false)).Length;
                for (int i = 0; i < space; i++)
                {
                    spaceStr += " ";
                }
                
                
                topList += $"{await anUser.GetName(false)} {spaceStr}|   Level {anUser.GetLevel()}; {anUser.Exp}xp\n";
            }

            topList += "```";

            await RespondAsync(topList);
        }
    }
}