using Discord;
using Discord.Interactions;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
    public class FishCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("fish", "Try to catch a fish")]
        public async Task CatchFish()
        {
            if (!await FishEngine.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
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

        [SlashCommand("fish-make", "Use your fish to make something.")]
        public async Task FishMake(string option = "none")
        {
            if (!await FishEngine.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }

            string choice = option.ToLower();

            if (FishEngine.IsValidMakeChoice(choice))
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                string ingredientsOrSuccess = await FishEngine.HasIngredients(dbUser, choice);
                if (ingredientsOrSuccess != "success")
                {
                    await RespondAsync($"You need at least {ingredientsOrSuccess} to make {FishEngine.GetItemName(choice)}.", ephemeral: true);
                    return;
                }

                ingredientsOrSuccess = FishEngine.MakeItem(dbUser, choice);

                await RespondAsync($"[{Context.User.Username}] You have spent {ingredientsOrSuccess} to make: {FishEngine.GetItemName(choice)}");
                await ReplyAsync($"1 {FishEngine.GetItemName(choice)} added to inventory.");
            }
            else
            {
                await RespondAsync("Valid things to make: sushi, shrimprice", ephemeral: true);
                return;
            }
        }

        [SlashCommand("fish-give", "Give an item to another user.")]
        public async Task FishGive(IUser user, string option = "none")
        {
            if (!await FishEngine.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }

            string choice = option.ToLower();

            if (FishEngine.IsValidGiveChoice(choice))
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                var receiver = await UserEngine.GetDBUser(user);

                if (await FishEngine.GetItem(dbUser, choice) < 1)
                {
                    await RespondAsync($"You don't have any {FishEngine.GetItemName(choice)} to give.");
                    return;
                }

                FishEngine.ModifyItem(receiver, choice, +1);

                FishEngine.ModifyItem(dbUser, choice, -1);

                await RespondAsync($"[{Context.User.Username}] have given {FishEngine.GetItemName(choice)} to {user.Mention}!");
            }
            else
            {
                await RespondAsync("Valid things to give: sushi, shrimprice", ephemeral: true);
                return;
            }
        }

        [SlashCommand("fish-use", "Use an item, optionally with another user.")]
        public async Task FishUse(string option = "none", IUser? user = null)
        {
            if (!await FishEngine.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }

            var dbUser = await UserEngine.GetDBUser(Context.User);

            string choice = option.ToLower();

            if (choice == "sushi" || choice == "shrimprice")
            {
                if (await FishEngine.GetItem(dbUser, choice) < 1)
                {
                    await RespondAsync($"You don't have any {FishEngine.GetItemName(choice)} to use.");
                    return;
                }

                FishEngine.ModifyItem(dbUser, choice, -1);

                if (user is null)
                {
                    await RespondAsync($"[{Context.User.Username}] has eaten {FishEngine.GetItemName(choice)}.");
                }
                else
                {
                    await RespondAsync($"[{Context.User.Username}] has eaten {FishEngine.GetItemName(choice)}, and shared some with {user.Mention}.");
                }
            }
            if (choice == "garbage")
            {
                if (await FishEngine.GetItem(dbUser, "garbage") < 1)
                {
                    await RespondAsync($"You don't have any {FishEngine.GetItemName(choice)} to use.");
                    return;
                }

                if (user is null)
                {
                    await RespondAsync($"'Using' garbage requires tagging another member to throw the trash at.", ephemeral: true);
                }
                else
                {
                    FishEngine.ModifyItem(dbUser, "garbage", -1);
                    await RespondAsync($"[{Context.User.Username}] has thrown some {FishEngine.GetItemName("garbage")} at {user.Mention}. Well done!");
                }
            }
            else
            {
                await RespondAsync("Valid things to use: sushi, shrimprice, garbage", ephemeral: true);
                return;
            }
        }

        [SlashCommand("fish-inventory", "Check your fish inventory")]
        public async Task FishInventory()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("Fish commands don't work in DM's.");
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
                $"Catch", 
                $"{FishEngine.GetItemName("fish")}: {await FishEngine.GetItem(user, "fish")} \n" +
                $"{FishEngine.GetItemName("uncommonfish")}: {await FishEngine.GetItem(user, "uncommonfish")} \n" +
                $"{FishEngine.GetItemName("rarefish")}: {await FishEngine.GetItem(user, "rarefish")} \n" +
                $"{FishEngine.GetItemName("shrimp")}: {await FishEngine.GetItem(user, "shrimp")}");
            embed.AddField(
                $"Items",
                $"{FishEngine.GetItemName("garbage")}: {await FishEngine.GetItem(user, "garbage")}\n" +
                $"{FishEngine.GetItemName("rice")}: {await FishEngine.GetItem(user, "rice")}\n" +
                $"{FishEngine.GetItemName("sushi")}: {await FishEngine.GetItem(user, "sushi")}\n" +
                $"{FishEngine.GetItemName("shrimprice")}: {await FishEngine.GetItem(user, "shrimprice")}");

            embed.Description = null;
            await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
        }

        [SlashCommand("fish-shop", "See items available in the fish shop, or provide an option to buy.")]
        public async Task FishShop(int option = -1)
        {
            if (option == -1)
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                if (dbUser is null) return;
                EmbedBuilder embed = new()
                {
                    Title = "Fish shop!",
                    Description = $"{Context.User.Username} currency: {await FishEngine.GetItem(dbUser, "rarefish")} {FishEngine.GetItemName("rarefish")} ; {await FishEngine.GetItem(dbUser, "garbage")} {FishEngine.GetItemName("garbage")}"
                };

                embed.AddField("Options:",
                    $"**1.** Buy [2 {FishEngine.GetItemName("rice")}] for [1 {FishEngine.GetItemName("rarefish")}]\n" +
                    $"**2.** Buy [1 {FishEngine.GetItemName("fish")}] for [2 {FishEngine.GetItemName("garbage")}]\n" +
                    $"**3.** Buy [1 {FishEngine.GetItemName("uncommonfish")}] for [5 {FishEngine.GetItemName("garbage")}]\n");

                await RespondAsync(embed: embed.Build());
                return;
            }
            else
            {
                var user = await UserEngine.GetDBUser(Context.User);
                await RespondAsync(await FishEngine.ShopBuy(user, option, Context.User.Username));
            }
        }

        [SlashCommand("fish-leader", "List the top fishers by total fish in their inventory")]
        public async Task FishTops()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("Fish commands don't work in DM's.");
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
            var getSushiTasks = users.Select(x => FishEngine.GetItem(x, "sushi")).ToList();
            var getShrimpRiceTasks = users.Select(x => FishEngine.GetItem(x, "shrimprice")).ToList();
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