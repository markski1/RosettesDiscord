using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
            var dbUser = await UserEngine.GetDBUser(Context.User);
            if (!dbUser.CanFish())
            {
                await RespondAsync("You can only fish every 60 minutes.");
                return;
            }
            EmbedBuilder embed = Global.MakeRosettesEmbed(Context.User);
            embed.Title = "Fishing! 🎣";
            EmbedFieldBuilder fishField = new()
            {
                Name = "Catching...",
                Value = "`[|||       ]`"
            };
            embed.AddField(fishField);
            await RespondAsync(embed: embed.Build());

            await Task.Delay(250);

            fishField.Value = "`[||||||    ]`";
            await ModifyOriginalResponseAsync(x => x.Embed = embed.Build());

            await Task.Delay(250);

            fishField.Value = "`[||||||||| ]`";
            await ModifyOriginalResponseAsync(x => x.Embed = embed.Build());

            await Task.Delay(200);

            Random rand = new();
            int caught = rand.Next(100);
            string fishingCatch = caught switch
            {
                (<= 35) => "fish",
                (> 35 and <= 55) => "uncommonfish",
                (> 55 and <= 65) => "rarefish",
                (> 65 and < 85) => "shrimp",
                _ => "garbage"
            };

            fishField.Name = "You caught:";
            fishField.Value = RpgEngine.GetItemName(fishingCatch);

            embed.Footer = new EmbedFooterBuilder()
            {
                Text = $"added to inventory."
            };

            RpgEngine.ModifyItem(dbUser, fishingCatch, +1);

            await ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
        }

        [SlashCommand("craft", "Combine your items to make something.")]
        public async Task RpgMake(string option = "none")
        {
            string isAllowed = await RpgEngine.CanuseRPGCommand(Context);
            if (isAllowed != "yes")
            {
                await RespondAsync(isAllowed, ephemeral: true);
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

                EmbedBuilder embed = Global.MakeRosettesEmbed(Context.User);
                embed.Title = "Crafted new item.";

                ingredientsOrSuccess = RpgEngine.MakeItem(dbUser, choice);

                embed.AddField("Spent:", ingredientsOrSuccess);
                embed.AddField("Made:", RpgEngine.GetItemName(choice));

                embed.Footer = new EmbedFooterBuilder()
                {
                    Text = $"added to inventory."
                };

                await RespondAsync(embed: embed.Build());
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

                EmbedBuilder embed = Global.MakeRosettesEmbed();
                embed.Title = "Item given.";

                embed.AddField(Context.User.Mention, $"Given {RpgEngine.GetItemName(choice)} to {user.Mention}!");

                await RespondAsync(embed: embed.Build());
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
            string isAllowed = await RpgEngine.CanuseRPGCommand(Context);
            if (isAllowed != "yes")
            {
                await RespondAsync(isAllowed, ephemeral: true);
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

                EmbedBuilder embed = Global.MakeRosettesEmbed(Context.User);
                embed.Title = "Item consumed.";

                if (user is null)
                {
                    embed.Description = $"Has eaten {RpgEngine.GetItemName(choice)}";
                }
                else
                {
                    embed.Description = $"Has eaten {RpgEngine.GetItemName(choice)}, and shared some with {user.Mention}.";
                }

                await RespondAsync(embed: embed.Build());
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
            string isAllowed = await RpgEngine.CanuseRPGCommand(Context);
            if (isAllowed != "yes")
            {
                await RespondAsync(isAllowed, ephemeral: true);
                return;
            }

            EmbedBuilder embed = Global.MakeRosettesEmbed(Context.User);
            embed.Title = $"Inventory";
            embed.Description = "Loading inventory...";

            await RespondAsync(embed: embed.Build());

            User user = await UserEngine.GetDBUser(Context.User);

            List<string> fieldsToList = new();

            EmbedFooterBuilder footer = new() { Text = $"================= Wallet: {await RpgEngine.GetItem(user, "dabloons")} {RpgEngine.GetItemName("dabloons")} =================" };

            embed.Footer = footer;

            fieldsToList.Add("garbage");
            fieldsToList.Add("rice");

            embed.AddField(
                $"Items",
                await RpgEngine.ListItems(user, fieldsToList),
                false
            );

            fieldsToList.Clear();
            fieldsToList.Add("fish");
            fieldsToList.Add("uncommonfish");
            fieldsToList.Add("rarefish");
            fieldsToList.Add("shrimp");

            embed.AddField(
                $"Catch",
                await RpgEngine.ListItems(user, fieldsToList),
                true
            );

            fieldsToList.Clear();
            fieldsToList.Add("sushi");
            fieldsToList.Add("shrimprice");

            embed.AddField(
                $"Finished Goods",
                await RpgEngine.ListItems(user, fieldsToList),
                true
            );

            embed.Description = null;

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
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
            var dbUser = await UserEngine.GetDBUser(Context.User);
            if (dbUser is null) return;
            EmbedBuilder embed = Global.MakeRosettesEmbed();
            embed.Title = "Rosettes shop!";
            embed.Description = $"The shop allows for buying and selling items for doubloons.";

            embed.Footer = new EmbedFooterBuilder() { Text = $"[{ Context.User.Username }] has: { await RpgEngine.GetItem(dbUser, "dabloons")} { RpgEngine.GetItemName("dabloons")}" };

            var comps = new ComponentBuilder();

            SelectMenuBuilder buyMenu = new()
            {
                Placeholder = "Buy...",
                CustomId = "buy"
            };
            buyMenu.AddOption(label: $"2 {RpgEngine.GetItemName("rice")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "buy1");
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("fish")}", description: $"2 {RpgEngine.GetItemName("dabloons")}", value: "buy2");
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("uncommonfish")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "buy3");

            SelectMenuBuilder sellMenu = new()
            {
                Placeholder = "Sell...",
                CustomId = "sell"
            };
            sellMenu.AddOption(label: $"1 {RpgEngine.GetItemName("rarefish")}]", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell1");
            sellMenu.AddOption(label: $"5 {RpgEngine.GetItemName("garbage")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell2");

            comps.WithSelectMenu(buyMenu, 0);
            comps.WithSelectMenu(sellMenu, 0);

            await RespondAsync(embed: embed.Build(), components: comps.Build());
            return;
        }

        [SlashCommand("food-leader", "Leaderbord by amount of food made.")]
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