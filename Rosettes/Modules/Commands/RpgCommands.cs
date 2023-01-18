using Discord;
using Discord.Interactions;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
    [Group("rpg", "RPG system commands")]
    public class RpgCommands : InteractionModuleBase<SocketInteractionContext>
    {

        [SlashCommand("fish", "Try to catch a fish")]
        public async Task CatchFish()
        {
            if (!await RpgEngine.CanuseRPGCommand(Context))
            {
                await RespondAsync("Sorry, RPG commands are disabled in this server.", ephemeral: true);
                return;
            }
            var dbUser = await UserEngine.GetDBUser(Context.User);
            if (!dbUser.CanFish())
            {
                await RespondAsync("You can only fish every 60 minutes.");
                return;
            }
            await RespondAsync($"[{Context.User.Username}] Fishing! 🎣");
            var message = await ReplyAsync("You caught");
            _ = new StartFishing(message, dbUser);
        }

        [SlashCommand("craft", "Combine your items to make something.")]
        public async Task RpgMake(string option = "none")
        {
            if (!await RpgEngine.CanuseRPGCommand(Context))
            {
                await RespondAsync("Sorry, RPG commands are disabled in this server.", ephemeral: true);
                return;
            }

            string choice = option.ToLower();

            if (RpgEngine.IsValidMakeChoice(choice))
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                string ingredientsOrSuccess = await RpgEngine.HasIngredients(dbUser, choice);
                if (ingredientsOrSuccess != "success")
                {
                    await RespondAsync($"You need at least {ingredientsOrSuccess} to make {RpgEngine.GetItemName(choice)}.", ephemeral: true);
                    return;
                }

                ingredientsOrSuccess = RpgEngine.MakeItem(dbUser, choice);

                await RespondAsync($"[{Context.User.Username}] You have spent {ingredientsOrSuccess} to make: {RpgEngine.GetItemName(choice)}");
                await ReplyAsync($"1 {RpgEngine.GetItemName(choice)} added to inventory.");
            }
            else
            {
                await RespondAsync("Valid things to make: sushi, shrimprice", ephemeral: true);
                return;
            }
        }

        [SlashCommand("give", "Give an item to another user.")]
        public async Task GiveItem(IUser user, string option = "none")
        {
            if (!await RpgEngine.CanuseRPGCommand(Context))
            {
                await RespondAsync("Sorry, RPG commands are disabled in this server.", ephemeral: true);
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

                await RespondAsync($"[{Context.User.Username}] have given {RpgEngine.GetItemName(choice)} to {user.Mention}!");
            }
            else
            {
                await RespondAsync("Valid things to give: sushi, shrimprice", ephemeral: true);
                return;
            }
        }

        [SlashCommand("use", "Use an item, optionally with another user.")]
        public async Task ItemUse(string option = "none", IUser? user = null)
        {
            if (!await RpgEngine.CanuseRPGCommand(Context))
            {
                await RespondAsync("Sorry, RPG commands are disabled in this server.", ephemeral: true);
                return;
            }

            var dbUser = await UserEngine.GetDBUser(Context.User);

            string choice = option.ToLower();

            if (choice == "sushi" || choice == "shrimprice")
            {
                if (await RpgEngine.GetItem(dbUser, choice) < 1)
                {
                    await RespondAsync($"You don't have any {RpgEngine.GetItemName(choice)} to use.");
                    return;
                }

                RpgEngine.ModifyItem(dbUser, choice, -1);

                if (user is null)
                {
                    await RespondAsync($"[{Context.User.Username}] has eaten {RpgEngine.GetItemName(choice)}.");
                }
                else
                {
                    await RespondAsync($"[{Context.User.Username}] has eaten {RpgEngine.GetItemName(choice)}, and shared some with {user.Mention}.");
                }
            }
            if (choice == "garbage")
            {
                if (await RpgEngine.GetItem(dbUser, "garbage") < 1)
                {
                    await RespondAsync($"You don't have any {RpgEngine.GetItemName(choice)} to use.");
                    return;
                }

                if (user is null)
                {
                    await RespondAsync($"'Using' garbage requires tagging another member to throw the trash at.", ephemeral: true);
                }
                else
                {
                    RpgEngine.ModifyItem(dbUser, "garbage", -1);
                    await RespondAsync($"[{Context.User.Username}] has thrown some {RpgEngine.GetItemName("garbage")} at {user.Mention}. Well done!");
                }
            }
            else
            {
                await RespondAsync("Valid things to use: sushi, shrimprice, garbage", ephemeral: true);
                return;
            }
        }

        [SlashCommand("inventory", "Check your inventory")]
        public async Task RpgInventory()
        {
            if (!await RpgEngine.CanuseRPGCommand(Context))
            {
                await RespondAsync("Sorry, RPG commands are disabled in this server.", ephemeral: true);
                return;
            }

            EmbedBuilder embed = new()
            {
                Title = $"{Context.User.Username}'s inventory",
                Description = "Loading inventory..."
            };

            await RespondAsync(embed: embed.Build());

            var user = await UserEngine.GetDBUser(Context.User);

            embed.AddField(
                $"Wallet",
                $"{await RpgEngine.GetItem(user, "dabloons")} {RpgEngine.GetItemName("dabloons")}");
            embed.AddField(
                $"Items",
                $"{RpgEngine.GetItemName("garbage")}: {await RpgEngine.GetItem(user, "garbage")}\n" +
                $"{RpgEngine.GetItemName("rice")}: {await RpgEngine.GetItem(user, "rice")}");
            embed.AddField(
                $"Catch", 
                $"{RpgEngine.GetItemName("fish")}: {await RpgEngine.GetItem(user, "fish")} \n" +
                $"{RpgEngine.GetItemName("uncommonfish")}: {await RpgEngine.GetItem(user, "uncommonfish")} \n" +
                $"{RpgEngine.GetItemName("rarefish")}: {await RpgEngine.GetItem(user, "rarefish")} \n" +
                $"{RpgEngine.GetItemName("shrimp")}: {await RpgEngine.GetItem(user, "shrimp")}");
            embed.AddField(
                $"Finished Goods",
                $"{RpgEngine.GetItemName("sushi")}: {await RpgEngine.GetItem(user, "sushi")}\n" +
                $"{RpgEngine.GetItemName("shrimprice")}: {await RpgEngine.GetItem(user, "shrimprice")}");

            embed.Description = null;
            await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
        }

        [SlashCommand("shop", "See items available in the shop, or provide an option to buy.")]
        public async Task RpgShop(int buy = -1, int sell = -1)
        {
            if (!await RpgEngine.CanuseRPGCommand(Context))
            {
                await RespondAsync("Sorry, RPG commands are disabled in this server.", ephemeral: true);
                return;
            }
            if (buy == -1 && sell == -1)
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                if (dbUser is null) return;
                EmbedBuilder embed = new()
                {
                    Title = "Rosettes shop!",
                    Description = $"[{Context.User.Username}] has: {await RpgEngine.GetItem(dbUser, "dabloons")} {RpgEngine.GetItemName("dabloons")}"
                };

                embed.AddField("Buy options:",
                    $"**1.** Buy [2 {RpgEngine.GetItemName("rice")}] for [5 {RpgEngine.GetItemName("dabloons")}]\n" +
                    $"**2.** Buy [1 {RpgEngine.GetItemName("fish")}] for [2 {RpgEngine.GetItemName("dabloons")}]\n" +
                    $"**3.** Buy [1 {RpgEngine.GetItemName("uncommonfish")}] for [5 {RpgEngine.GetItemName("dabloons")}]\n");

                embed.AddField("Sell otions:",
                    $"**1.** Sell [1 {RpgEngine.GetItemName("rarefish")}] for [5 {RpgEngine.GetItemName("dabloons")}]\n" +
                    $"**2.** Sell [5 {RpgEngine.GetItemName("garbage")}] for [5 {RpgEngine.GetItemName("dabloons")}]\n");

                await RespondAsync(embed: embed.Build());
                return;
            }
            else if (buy >= 0)
            {
                var user = await UserEngine.GetDBUser(Context.User);
                await RespondAsync(await RpgEngine.ShopBuy(user, buy, Context.User.Username));
            }
            else if (sell >= 0)
            {
                var user = await UserEngine.GetDBUser(Context.User);
                await RespondAsync(await RpgEngine.ShopSell(user, sell, Context.User.Username));
            }
        }

        [SlashCommand("food-leader", "Leaderbord by amount of food made.")]
        public async Task FoodLeaderboard()
        {
            if (!await RpgEngine.CanuseRPGCommand(Context))
            {
                await RespondAsync("Sorry, RPG commands are disabled in this server.", ephemeral: true);
                return;
            }
            var users = await UserEngine.GetAllUsersFromGuild(Context.Guild);

            if (users is null)
            {
                await RespondAsync("There was an error listing the top users in this guild, sorry.");
                return;
            }

            /*
             * Regarding the horrors ahead:
             * 
             * In any other scenario in the world, the following should be a single SQL query.
             * However, because one of the most important objectives of Rosettes is to not store more personal data than necesary,
             * and this includes not having to store what guilds a given user is in, this operation must happen in memory.
             * So we need to fetch sushi and shrimp of every user in the guild indivdiually in order to rank them.
             * This is very slow, but the database is always cached in memory and runs locally, so not a real as far as performance goes.
             * 
             */
            var getSushiTasks = users.Select(x => RpgEngine.GetItem(x, "sushi")).ToList();
            var getShrimpRiceTasks = users.Select(x => RpgEngine.GetItem(x, "shrimprice")).ToList();
            var userSushi = await Task.WhenAll(getSushiTasks);
            var userShrimpRice = await Task.WhenAll(getShrimpRiceTasks);
            var usersWithSushi = users.Zip(userSushi, (User, Sushi) => (User, Sushi));
            var usersWithSushiAndShrimp = usersWithSushi.Zip(userShrimpRice, (User, Shrimp) => (User, Shrimp));
            var topUsers = usersWithSushiAndShrimp.OrderByDescending(x => x.User.Sushi + x.Shrimp).Take(10);

            string topList = "Top 10 by combined finished food count: ```";
            topList += $"User                                    🍣    🍚\n\n";
            var spaceStr = "";
            var space = 30;

            foreach (var anUser in topUsers)
            {
                spaceStr = "";
                space = 32 - (await anUser.User.User.GetName(false)).Length;
                for (int i = 0; i < space; i++)
                {
                    spaceStr += " ";
                }
                var spaceBetweenStuff = "   ";
                if (anUser.User.Sushi < 100)
                {
                    spaceBetweenStuff += " ";
                    if (anUser.User.Sushi < 10)
                    {
                        spaceBetweenStuff += " ";
                    }
                }
                
                topList += $"{await anUser.User.User.GetName(false)} {spaceStr}|       {anUser.User.Sushi}{spaceBetweenStuff}{anUser.Shrimp}\n";
            }

            topList += "```";

            await RespondAsync(topList);
        }
    }
}