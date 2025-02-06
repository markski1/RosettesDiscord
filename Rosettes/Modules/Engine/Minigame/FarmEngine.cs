using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Modules.Engine.Guild;

namespace Rosettes.Modules.Engine.Minigame
{
    public static class FarmEngine
    {
        public static readonly FarmRepository _interface = new();

        public static readonly Dictionary<string, (string fullName, bool can_give, bool can_pet_eat)> inventoryItems = new()
        {
		//    db_name            name                   can_give  can_pet_eat
			{ "fish",           ( "🐡 Common fish",     true,    true   ) },
            { "uncommonfish",   ( "🐟 Uncommon fish",   true,    true   ) },
            { "rarefish",       ( "🐠 Rare fish",       true,    true   ) },
            { "shrimp",         ( "🦐 Shrimp",          true,    true   ) },
            { "dabloons",       ( "🐾 Dabloons",        true,    false  ) },
            { "garbage",        ( "🗑 Garbage",         true,    false  ) },
            { "tomato",         ( "🍅 Tomato",          true,    false  ) },
            { "carrot",         ( "🥕 Carrot",          true,    true   ) },
            { "potato",         ( "🥔 Potato",          true,    false  ) },
            { "seedbag",        ( "🌱 Seed bag",        true,    false  ) },
            { "fishpole",       ( "🎣 Fishing pole",    false,   false  ) },
            { "farmtools",      ( "🧰 Farming tools",   false,   false  ) },
            { "plots",          ( "🌿 Plot of land",    false,   false  ) },
            { "pets",           ( "[debug] pet list",   false,   false  ) }
        };

        public static readonly Dictionary<string, (string name, int amount, int cost)> itemSaleChart = new()
        {
		//   instruct   name         amount cost
			{ "sell1", ("fish",         5,   3) },
            { "sell2", ("uncommonfish", 5,   6) },
            { "sell3", ("rarefish",     1,   5) },
            { "sell4", ("shrimp",       5,   5) },
            { "sell5", ("tomato",       10,  6) },
            { "sell6", ("carrot",       10,  5) },
            { "sell7", ("potato",       10,  4) },
            { "sell8", ("garbage",      5,   3) }
        };

        public static string GetItemName(string choice)
        {
            if (!inventoryItems.ContainsKey(choice))
                return "invalid item";

            return inventoryItems[choice].fullName;
        }

        public static bool IsValidGiveChoice(string choice)
        {
            if (!inventoryItems.ContainsKey(choice))
                return false;

            return inventoryItems[choice].can_give;
        }

        public static bool IsValidItem(string choice)
        {
            return inventoryItems.ContainsKey(choice);
        }

        public static async void ModifyItem(User dbUser, string choice, int amount)
        {
            await FarmRepository.ModifyInventoryItem(dbUser, choice, amount);
        }

        public static async void SetItem(User dbUser, string choice, int newValue)
        {
            await FarmRepository.SetInventoryItem(dbUser, choice, newValue);
        }

        public static async void ModifyStrItem(User dbUser, string choice, string newValue)
        {
            await _interface.ModifyStrInventoryItem(dbUser, choice, newValue);
        }

        public static async Task<int> GetItem(User dbUser, string name)
        {
            return await FarmRepository.FetchInventoryItem(dbUser, name);
        }

        public static async Task<string> GetStrItem(User dbUser, string name)
        {
            return await FarmRepository.FetchInventoryStringItem(dbUser, name);
        }

        public static async Task<string> CanUseFarmCommand(SocketInteractionContext context)
        {
            if (context.Guild is null)
            {
                return "Farming/Fishing Commands do not work in direct messages.";
            }
            var dbGuild = await GuildEngine.GetDBGuild(context.Guild);
            if (!Global.CanSendMessage(context))
            {
                return "I don't have access to this channel. Please let an admin know, or try using me in other channel.";
            }
            if (!dbGuild.AllowsFarm())
            {
                return "This guild does not allow Farming/Fishing commands.";
            }
            if (dbGuild.LogChannel != 0 && dbGuild.LogChannel != context.Channel.Id)
            {
                return "Farming/Fishing commands are not allowed in this channel, please use the Game/Bot channel.";
            }
            return "yes";
        }

        public static async Task ShopAction(SocketMessageComponent component)
        {
            var dbUser = await UserEngine.GetDbUser(component.User);

            string text = "";

            switch (component.Data.CustomId)
            {
                case "buy":
                    switch (component.Data.Values.Last())
                    {
                        case "buy1":
                            text = await ItemBuy(dbUser, buying: "seedbag", amount: 1, cost: 5);
                            break;
                        case "buy2":
                            text = await ItemBuy(dbUser, buying: "seedbag", amount: 5, cost: 20);
                            break;
                        case "buy3":
                            if (await GetItem(dbUser, "fishpole") >= 25)
                            {
                                text = $"Your current {GetItemName("fishpole")} are still in good shape.";
                            }
                            else if (await GetItem(dbUser, "dabloons") >= 5)
                            {
                                ModifyItem(dbUser, "dabloons", -5);
                                SetItem(dbUser, "fishpole", 100);
                                text = $"You have purchased {GetItemName("fishpole")} for 5 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                text = $"You don't have 5 {GetItemName("dabloons")}";
                            }
                            break;
                        case "buy4":
                            if (await GetItem(dbUser, "farmtools") >= 25)
                            {
                                text = $"Your current {GetItemName("farmtools")} are still in good shape.";
                            }
                            else if (await GetItem(dbUser, "dabloons") >= 10)
                            {
                                ModifyItem(dbUser, "dabloons", -10);
                                SetItem(dbUser, "farmtools", 100);
                                text = $"You have purchased {GetItemName("farmtools")} for 10 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                text = $"You don't have 10 {GetItemName("dabloons")}";
                            }
                            break;
                        case "buy5":
                            if (await GetItem(dbUser, "dabloons") >= 200)
                            {
                                if (await GetItem(dbUser, "plots") >= 3)
                                {
                                    text = $"For the time being, you may not own more than 3 plots of land.";
                                }
                                else
                                {
                                    ModifyItem(dbUser, "dabloons", -200);
                                    ModifyItem(dbUser, "plots", +1);
                                    text = $"You have purchased a plot of land for 200 {GetItemName("dabloons")}";
                                }
                            }
                            else
                            {
                                text = $"You don't have 200 {GetItemName("dabloons")}";
                            }
                            break;
                    }
                    break;

                case "sell_e":
                case "sell":
                    bool sell_e = component.Data.CustomId.Contains("_e");

                    if (itemSaleChart.TryGetValue(component.Data.Values.Last(), out var values))
                    {
                        text = await ItemSell(dbUser, selling: values.name, amount: values.amount, cost: values.cost, everything: sell_e);
                    }

                    break;
            }

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Description = text;

            await component.RespondAsync(embed: embed.Build(), ephemeral: true);

            // reset the shop component options
            try
            {
                await component.Message.ModifyAsync(x => x.Components = GetShopComponents(empty: true).Build());
                await component.Message.ModifyAsync(x => x.Components = GetShopComponents().Build());
            }
            catch
            {
                // nothing we can do at this point, just don't crash.
            }
        }

        public static async Task<string> ItemBuy(User dbUser, string buying, int amount, int cost, bool setType = false)
        {
            if (await GetItem(dbUser, "dabloons") >= cost)
            {
                ModifyItem(dbUser, "dabloons", -cost);
                if (setType)
                {
                    SetItem(dbUser, buying, amount);
                }
                else
                {
                    ModifyItem(dbUser, buying, +amount);
                }
                return $"You have purchased {amount} {GetItemName("seedbag")} for {cost} {GetItemName("dabloons")}";
            }
            else
            {
                return $"You don't have {cost} {GetItemName("dabloons")}";
            }
        }

        public static async Task<string> ItemSell(User dbUser, string selling, int amount, int cost, bool everything)
        {
            int availableAmount = await GetItem(dbUser, selling);
            if (availableAmount >= amount)
            {
                if (everything)
                {
                    int timesToSell = availableAmount / amount;

                    int totalSold = amount * timesToSell;
                    int totalEarned = cost * timesToSell;

                    // shouldn't happen, but...
                    while (totalSold > availableAmount)
                    {
                        totalSold -= amount;
                    }

                    ModifyItem(dbUser, selling, -totalSold);
                    ModifyItem(dbUser, "dabloons", +totalEarned);

                    return $"You have sold {totalSold} {GetItemName(selling)} for {totalEarned} {GetItemName("dabloons")}";
                }
                else
                {
                    ModifyItem(dbUser, selling, -amount);
                    ModifyItem(dbUser, "dabloons", +cost);
                    return $"You have sold {amount} {GetItemName(selling)} for {cost} {GetItemName("dabloons")}";
                }
            }
            else
            {
                return $"You don't have enough {GetItemName(selling)} to sell.";
            }
        }

        public static async Task<string> ListItems(User user, List<string> items)
        {
            string list = "";

            foreach (var item in items)
            {
                int amount = await GetItem(user, item);

                if (item is "fishpole" or "farmtools")
                {
                    if (amount <= 0)
                    {
                        list += $"{GetItemName(item)}: `broken`\n";
                    }
                    else
                    {
                        list += $"{GetItemName(item)}: `{amount}% status`\n";
                    }
                }
                else if (amount != 0)
                {
                    list += $"{GetItemName(item)}: {amount}\n";
                }
            }

            if (list == "")
            {
                list = "Nothing.";
            }

            return list;
        }

        // main funcs

        public static async Task CatchFishFunc(SocketInteraction interaction, IUser user)
        {
            var dbUser = await UserEngine.GetDbUser(user);

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            AddStandardButtons(ref buttonRow);

            comps.AddRow(buttonRow);

            var poleStatus = await GetItem(dbUser, "fishpole");

            if (poleStatus <= 0)
            {
                embed.Title = $"{GetItemName("fishpole")} broken.";
                embed.Description = $"Your {GetItemName("fishpole")} broke, you need a new one.";

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build(), ephemeral: true);
                return;
            }

            if (!dbUser.CanFish())
            {
                embed.Title = "Can't fish yet.";
                embed.Description = $"You may fish again <t:{dbUser.GetFishTime()}:R>";

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build(), ephemeral: true);
                return;
            }

            embed.Title = "Fishing! 🎣";

            int caught = Global.Randomize(100);
            string fishingCatch;

            int expIncrease;

            switch (caught)
            {
                case <= 40:
                    fishingCatch = "fish";
                    expIncrease = 10;
                    break;
                case > 40 and <= 60:
                    fishingCatch = "uncommonfish";
                    expIncrease = 15;
                    break;
                case > 60 and <= 70:
                    fishingCatch = "rarefish";
                    expIncrease = 18;
                    break;
                case > 70 and < 90:
                    fishingCatch = "shrimp";
                    expIncrease = 12;
                    break;
                default:
                    fishingCatch = "garbage";
                    expIncrease = 8;
                    break;
            }

            EmbedFieldBuilder fishField = new()
            {
                Name = "You caught:",
                Value = GetItemName(fishingCatch)
            };
            embed.AddField(fishField);


            ModifyItem(dbUser, fishingCatch, +1);

            int foundPet = await PetEngine.RollForPet(dbUser);

            if (foundPet > 0)
            {
                embed.AddField("You found a pet.", $"While fishing, you found a friendly {PetEngine.PetNames(foundPet)}, who chased you about. It has been added to your pets.");
                buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);
                expIncrease *= 5;
                expIncrease /= 2;
            }

            int damage = 3 + Global.Randomize(4);

            poleStatus -= damage;

            ModifyItem(dbUser, "fishpole", -damage);

            if (poleStatus <= 0)
            {
                embed.AddField($"{GetItemName("fishpole")} destroyed.", $"Your {GetItemName("fishpole")} broke during this activity, you must get a new one.");
            }

            embed.Footer = new EmbedFooterBuilder()
            {
                Text = $"{dbUser.AddExp(expIncrease)} | added to inventory."
            };

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }

        public static async Task ShowInventoryFunc(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDbUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            await interaction.DeferAsync();

            List<string> fieldsToList = new();

            EmbedFooterBuilder footer = new() { Text = $"{await GetItem(dbUser, "dabloons")} {GetItemName("dabloons")} | {await GetItem(dbUser, "seedbag")} {GetItemName("seedbag")}\n{dbUser.Exp} experience" };

            embed.Footer = footer;

            fieldsToList.Add("garbage");
            fieldsToList.Add("fishpole");
            fieldsToList.Add("farmtools");

            embed.AddField(
                $"Items",
                await ListItems(dbUser, fieldsToList),
                false
            );

            fieldsToList.Clear();
            fieldsToList.Add("fish");
            fieldsToList.Add("uncommonfish");
            fieldsToList.Add("rarefish");
            fieldsToList.Add("shrimp");

            embed.AddField(
                $"Catch",
                await ListItems(dbUser, fieldsToList),
                true
            );

            fieldsToList.Clear();
            fieldsToList.Add("tomato");
            fieldsToList.Add("carrot");
            fieldsToList.Add("potato");

            embed.AddField(
                $"Harvest",
                await ListItems(dbUser, fieldsToList),
                true
            );

            embed.Description = null;

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();
            ;
            AddStandardButtons(ref buttonRow, except: "inventory");
            buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            Pet? pet = await PetEngine.GetUserPet(dbUser);

            if (pet is not null)
            {
                ActionRowBuilder petRow = new();
                petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
                petRow.WithButton(label: $"{pet.GetEmoji()} information", customId: $"pet_view", style: ButtonStyle.Secondary);
                comps.AddRow(petRow);
            }

            await interaction.FollowupAsync(embed: embed.Build(), components: comps.Build());
        }

        public static async Task ShowShopFunc(SocketInteraction interaction, SocketUser user)
        {
            var dbUser = await UserEngine.GetDbUser(user);
            if (dbUser is null) return;

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
            embed.Description = $"The shop allows for buying and selling items for dabloons.";

            embed.Footer = new EmbedFooterBuilder() { Text = $"You have: {await GetItem(dbUser, "dabloons")} {GetItemName("dabloons")}" };

            var comps = GetShopComponents();

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }

        private static ComponentBuilder GetShopComponents(bool empty = false)
        {
            SelectMenuBuilder buyMenu = new()
            {
                Placeholder = "Buy...",
                CustomId = "buy",
                MinValues = 1,
                MaxValues = 1
            };
            if (!empty)
            {
                buyMenu.AddOption(label: $"1 {GetItemName("seedbag")}", description: $"5 {GetItemName("dabloons")}", value: "buy1");
                buyMenu.AddOption(label: $"5 {GetItemName("seedbag")}", description: $"20 {GetItemName("dabloons")}", value: "buy2");
                buyMenu.AddOption(label: $"1 {GetItemName("fishpole")}", description: $"5 {GetItemName("dabloons")}", value: "buy3");
                buyMenu.AddOption(label: $"1 {GetItemName("farmtools")}", description: $"10 {GetItemName("dabloons")}", value: "buy4");
                buyMenu.AddOption(label: $"1 🌿 Plot of land", description: $"200 {GetItemName("dabloons")}", value: "buy5");
            }
            else
            {
                buyMenu.AddOption(label: $"Please wait...", value: "NULL");
            }
            buyMenu.MaxValues = 1;

            SelectMenuBuilder sellMenu = new()
            {
                Placeholder = "Sell...",
                CustomId = "sell",
                MinValues = 1,
                MaxValues = 1
            };
            if (!empty)
            {
                sellMenu.AddOption(label: $"5 {GetItemName("fish")}", description: $"3 {GetItemName("dabloons")}", value: "sell1");
                sellMenu.AddOption(label: $"5 {GetItemName("uncommonfish")}", description: $"6 {GetItemName("dabloons")}", value: "sell2");
                sellMenu.AddOption(label: $"1 {GetItemName("rarefish")}", description: $"5 {GetItemName("dabloons")}", value: "sell3");
                sellMenu.AddOption(label: $"5 {GetItemName("shrimp")}", description: $"5 {GetItemName("dabloons")}", value: "sell4");
                sellMenu.AddOption(label: $"10 {GetItemName("tomato")}", description: $"6 {GetItemName("dabloons")}", value: "sell5");
                sellMenu.AddOption(label: $"10 {GetItemName("carrot")}", description: $"5 {GetItemName("dabloons")}", value: "sell6");
                sellMenu.AddOption(label: $"10 {GetItemName("potato")}", description: $"4 {GetItemName("dabloons")}", value: "sell7");
                sellMenu.AddOption(label: $"5 {GetItemName("garbage")}", description: $"3 {GetItemName("dabloons")}", value: "sell8");
            }
            else
            {
                sellMenu.AddOption(label: $"Please wait...", value: "NULL");
            }
            sellMenu.MaxValues = 1;

            SelectMenuBuilder sellAllMenu = new()
            {
                Placeholder = "Sell everything of...",
                CustomId = "sell_e",
                MinValues = 1,
                MaxValues = 1
            };
            if (!empty)
            {
                sellAllMenu.AddOption(label: GetItemName("fish"), description: $"3 {GetItemName("dabloons")} per every 5", value: "sell1");
                sellAllMenu.AddOption(label: GetItemName("uncommonfish"), description: $"6 {GetItemName("dabloons")} per every 5", value: "sell2");
                sellAllMenu.AddOption(label: GetItemName("rarefish"), description: $"5 {GetItemName("dabloons")} per every 1", value: "sell3");
                sellAllMenu.AddOption(label: GetItemName("shrimp"), description: $"5 {GetItemName("dabloons")} per every 5", value: "sell4");
                sellAllMenu.AddOption(label: GetItemName("tomato"), description: $"6 {GetItemName("dabloons")} per every 10", value: "sell5");
                sellAllMenu.AddOption(label: GetItemName("carrot"), description: $"5 {GetItemName("dabloons")} per every 10", value: "sell6");
                sellAllMenu.AddOption(label: GetItemName("potato"), description: $"4 {GetItemName("dabloons")} per every 10", value: "sell7");
                sellAllMenu.AddOption(label: GetItemName("garbage"), description: $"3 {GetItemName("dabloons")} per every 5", value: "sell8");
            }
            else
            {
                sellAllMenu.AddOption(label: $"Please wait...", value: "NULL");
            }
            sellAllMenu.MaxValues = 1;

            ActionRowBuilder buttonRow = new();

            AddStandardButtons(ref buttonRow, except: "shop");

            return
                new ComponentBuilder()
                .WithSelectMenu(buyMenu, 0)
                .WithSelectMenu(sellMenu, 0)
                .WithSelectMenu(sellAllMenu, 0)
                .AddRow(buttonRow);
        }

        public static void AddStandardButtons(ref ActionRowBuilder buttonRow, string except = "none")
        {
            if (except != "fish") buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary);
            if (except != "farm") buttonRow.WithButton(label: "Farm", customId: "farm", style: ButtonStyle.Primary);
            if (except != "shop") buttonRow.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Secondary);
            if (except != "inventory") buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);
        }

    }
}