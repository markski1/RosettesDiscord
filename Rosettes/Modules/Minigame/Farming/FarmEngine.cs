using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Guild;
using Rosettes.Modules.Minigame.Pets;

namespace Rosettes.Modules.Minigame.Farming;

public static class FarmEngine
{
    // Embed color palette.
    public static readonly Color FarmColor    = new(86, 184, 94);   // soft green
    public static readonly Color WaterColor   = new(64, 156, 220);  // soft blue
    public static readonly Color HarvestColor = Color.Gold;
    public static readonly Color FishColor    = new(64, 156, 220);
    public static readonly Color ShopColor    = Color.Gold;
    public static readonly Color ErrorColor   = new(220, 80, 80);   // muted red

    // 5-slot durability bar, e.g. ▰▰▰▱▱ for 60.
    public static string DurabilityBar(int percent)
    {
        if (percent < 0) percent = 0;
        if (percent > 100) percent = 100;
        int filled = (int)Math.Round(percent / 20.0);
        return new string('▰', filled) + new string('▱', 5 - filled);
    }

    public static readonly Dictionary<string, (string fullName, bool can_give, bool can_pet_eat)> InventoryItems = new()
    {
        // db_name / (name / can_give / can_pet_eat)
        { "fish",           ( "🐡 Common fish",     true,    true   ) },
        { "uncommonfish",   ( "🐟 Uncommon fish",   true,    true   ) },
        { "rarefish",       ( "🐠 Rare fish",       true,    true   ) },
        { "shrimp",         ( "🦐 Shrimp",          true,    true   ) },
        { "dabloons",       ( "🐾 Dabloons",        true,    false  ) },
        { "garbage",        ( "🗑 Garbage",          true,    false  ) },
        { "tomato",         ( "🍅 Tomato",          true,    false  ) },
        { "carrot",         ( "🥕 Carrot",          true,    true   ) },
        { "potato",         ( "🥔 Potato",          true,    false  ) },
        { "seedbag",        ( "🌱 Seed bag",        true,    false  ) },
        { "fishpole",       ( "🎣 Fishing pole",    false,   false  ) },
        { "farmtools",      ( "🧰 Farming tools",   false,   false  ) },
        { "plots",          ( "🌿 Plot of land",    false,   false  ) },
    };

    private static readonly Dictionary<string, (int amount, int cost)> ItemSaleChart = new()
    {
        // name / (amount / cost)
        { "fish",         (5,     3) },
        { "uncommonfish", (5,     6) },
        { "rarefish",     (1,     5) },
        { "shrimp",       (5,     5) },
        { "tomato",       (10,    6) },
        { "carrot",       (10,    5) },
        { "potato",       (10,    4) },
        { "garbage",      (5,     3) }
    };

    private static readonly Dictionary<string, int> ItemBuyChart = new()
    {
        // name / price
        { "seedbag",      5 },
        { "fishpole",     10 },
        { "farmtools",    15 },
        { "plots",        200 }
    };

    public static string GetItemName(string choice)
    {
        if (!InventoryItems.TryGetValue(choice, out var item))
            return "invalid item";

        return item.fullName;
    }

    public static bool IsValidGiveChoice(string choice)
    {
        if (!InventoryItems.TryGetValue(choice, out var item))
            return false;

        return item.can_give;
    }

    public static bool IsValidItem(string choice)
    {
        return InventoryItems.ContainsKey(choice);
    }

    public static Task ModifyItem(User dbUser, string choice, int amount) =>
        FarmRepository.ModifyInventoryItem(dbUser, choice, amount);

    private static Task SetItem(User dbUser, string choice, int newValue) =>
        FarmRepository.SetInventoryItem(dbUser, choice, newValue);

    public static async Task<int> GetItem(User dbUser, string name)
    {
        return await FarmRepository.FetchInventoryItem(dbUser, name);
    }

    public static async Task<string> CanUseFarmCommand(SocketInteractionContext context)
    {
        if (context.Guild is null)
        {
            return "Farming/Fishing Commands do not work in direct messages.";
        }
        var dbGuild = await GuildEngine.GetDbGuild(context.Guild);
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

        var raw = component.Data.Values.LastOrDefault();
        var parts = string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(':', StringSplitOptions.TrimEntries);

        string text = "";

        switch (component.Data.CustomId)
        {
            // A purchase is composed by the name of the item purchased, and its amount (i.e., seedbag:5, fishpole:1)
            case "buy" when parts.Length == 2:
            {
                string item = parts[0];

                if (!int.TryParse(parts[1], out var amount) || amount <= 0)
                {
                    text = "There was an error parsing your shop request.";
                    break;
                }

                if (!ItemBuyChart.TryGetValue(item, out var price))
                {
                    text = "There was an error parsing your shop request.";
                    break;
                }

                if (await GetItem(dbUser, "dabloons") < price)
                {
                    text = $"You don't have {price} {GetItemName("dabloons")}";
                    break;
                }

                text = $"You have purchased {GetItemName(item)} for {price} {GetItemName("dabloons")}";

                switch (item)
                {
                    case "fishpole":
                        if (await GetItem(dbUser, "fishpole") >= 30)
                        {
                            text = $"Your current {GetItemName("fishpole")} is still in good shape.";
                            break;
                        }
                        Global.FireAndForget(ModifyItem(dbUser, "dabloons", -10));
                        Global.FireAndForget(SetItem(dbUser, "fishpole", 100));
                        break;
                    case "farmtools":
                        if (await GetItem(dbUser, "farmtools") >= 30)
                        {
                            text = $"Your current {GetItemName("farmtools")} are still in good shape.";
                            break;
                        }
                        Global.FireAndForget(ModifyItem(dbUser, "dabloons", -15));
                        Global.FireAndForget(SetItem(dbUser, "farmtools", 100));
                        break;
                    case "plots":
                        if (await GetItem(dbUser, "plots") >= 3)
                        {
                            text = "For the time being, you may not own more than 3 plots of land.";
                            break;
                        }
                        Global.FireAndForget(ModifyItem(dbUser, "dabloons", -200));
                        Global.FireAndForget(ModifyItem(dbUser, "plots", +1));
                        break;
                    default:
                        text = ItemBuy(dbUser, boughtItem: item, amount: amount, cost: price * amount);
                        break;
                }
                break;
            }

            // A sale is a preset 'operation' with an ID (i.e., sell1, sell2).
            case "sell_e" when parts.Length == 1:
            case "sell" when parts.Length == 1:
            {
                string item = parts[0];

                bool sellEverything = component.Data.CustomId.Contains("_e");

                if (ItemSaleChart.TryGetValue(item, out var values))
                {
                    text = await ItemSell(dbUser, selling: item, amount: values.amount, cost: values.cost, everything: sellEverything);
                }

                break;
            }
        }

        bool success = text.StartsWith("You have purchased") || text.StartsWith("You have sold");

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser, success ? ShopColor : ErrorColor);
        Global.AddTitle(container, success ? "### 🛒 Transaction complete" : "### 🛒 Shop");
        container.WithTextDisplay(text);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await component.RespondAsync(components: comps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);

        // reset the shop component options
        try
        {
            int dabloons = await GetItem(dbUser, "dabloons");

            ContainerBuilder resetContainer = await Global.MakeRosettesContainer(dbUser, ShopColor);
            Global.AddTitle(resetContainer, "### 🛒 Shop");
            resetContainer.WithTextDisplay("Use the menus below to buy or sell items.");
            Global.AddFooter(resetContainer, $"🐾 {dabloons} dabloons");
            GetShopComponentsV2(resetContainer);
            await Global.AddAuthorFooter(resetContainer, dbUser);

            ComponentBuilderV2 resetComps = new();
            resetComps.WithContainer(resetContainer);
            await component.Message.ModifyAsync(x =>
            {
                x.Components = resetComps.Build();
                x.Flags = MessageFlags.ComponentsV2;
            });

            ContainerBuilder resetContainer2 = await Global.MakeRosettesContainer(dbUser, ShopColor);
            Global.AddTitle(resetContainer2, "### 🛒 Shop");
            resetContainer2.WithTextDisplay("Use the menus below to buy or sell items.");
            Global.AddFooter(resetContainer2, $"🐾 {dabloons} dabloons");
            GetShopComponentsV2(resetContainer2);
            await Global.AddAuthorFooter(resetContainer2, dbUser);

            ComponentBuilderV2 resetComps2 = new();
            resetComps2.WithContainer(resetContainer2);
            await component.Message.ModifyAsync(x =>
            {
                x.Components = resetComps2.Build();
                x.Flags = MessageFlags.ComponentsV2;
            });
        }
        catch
        {
            // nothing we can do at this point, just don't crash.
        }
    }

    private static string ItemBuy(User dbUser, string boughtItem, int amount, int cost, bool setType = false)
    {
        Global.FireAndForget(ModifyItem(dbUser, "dabloons", -cost));
        if (setType)
        {
            Global.FireAndForget(SetItem(dbUser, boughtItem, amount));
        }
        else
        {
            Global.FireAndForget(ModifyItem(dbUser, boughtItem, +amount));
        }
        return $"You have purchased {amount} {GetItemName(boughtItem)} for {cost} {GetItemName("dabloons")}";
    }

    private static async Task<string> ItemSell(User dbUser, string selling, int amount, int cost, bool everything)
    {
        int availableAmount = await GetItem(dbUser, selling);
        if (availableAmount < amount) return $"You don't have enough {GetItemName(selling)} to sell.";

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

            Global.FireAndForget(ModifyItem(dbUser, selling, -totalSold));
            Global.FireAndForget(ModifyItem(dbUser, "dabloons", +totalEarned));

            return $"You have sold {totalSold} {GetItemName(selling)} for {totalEarned} {GetItemName("dabloons")}";
        }

        Global.FireAndForget(ModifyItem(dbUser, selling, -amount));
        Global.FireAndForget(ModifyItem(dbUser, "dabloons", +cost));
        return $"You have sold {amount} {GetItemName(selling)} for {cost} {GetItemName("dabloons")}";

    }

    private static async Task<string> ListItems(User user, List<string> items)
    {
        string list = "";

        foreach (var item in items)
        {
            int amount = await GetItem(user, item);

            if (item is "fishpole" or "farmtools")
            {
                if (amount <= 0)
                {
                    list += $"{GetItemName(item)}\n`▱▱▱▱▱` *broken*\n";
                }
                else
                {
                    list += $"{GetItemName(item)}\n`{DurabilityBar(amount)}` {amount}%\n";
                }
            }
            else if (amount != 0)
            {
                list += $"{GetItemName(item)} × **{amount}**\n";
            }
        }

        if (list == "")
        {
            list = "*Nothing.*";
        }

        return list;
    }

    // main funcs

    public static async Task CatchFishFunc(SocketInteraction interaction, IUser user)
    {
        var dbUser = await UserEngine.GetDbUser(user);

        var poleStatus = await GetItem(dbUser, "fishpole");

        if (poleStatus <= 0)
        {
            ContainerBuilder errorContainer = await Global.MakeRosettesContainer(dbUser, ErrorColor);
            errorContainer.WithTextDisplay($"🎣 {GetItemName("fishpole")} broken");
            errorContainer.WithTextDisplay($"Your {GetItemName("fishpole")} is broken. Pick up a new one at the shop.");

            ActionRowBuilder errorRow = new();
            AddStandardButtons(ref errorRow);
            errorContainer.WithActionRow(errorRow);

            ComponentBuilderV2 errorComps = new();
            errorComps.WithContainer(errorContainer);

            await interaction.RespondAsync(components: errorComps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
            return;
        }

        if (!dbUser.CanFish())
        {
            ContainerBuilder errorContainer = await Global.MakeRosettesContainer(dbUser, ErrorColor);
            errorContainer.WithTextDisplay("🎣 Can't fish yet");
            errorContainer.WithTextDisplay($"You may fish again <t:{dbUser.GetFishTime()}:R>.");

            ActionRowBuilder errorRow = new();
            AddStandardButtons(ref errorRow);
            errorContainer.WithActionRow(errorRow);

            ComponentBuilderV2 errorComps = new();
            errorComps.WithContainer(errorContainer);

            await interaction.RespondAsync(components: errorComps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
            return;
        }

        int caught = Global.Randomize(100);
        string fishingCatch;

        int expIncrease;

        switch (caught)
        {
            case <= 40:
                fishingCatch = "fish";
                expIncrease = 10;
                break;
            case <= 60:
                fishingCatch = "uncommonfish";
                expIncrease = 15;
                break;
            case <= 70:
                fishingCatch = "rarefish";
                expIncrease = 18;
                break;
            case < 90:
                fishingCatch = "shrimp";
                expIncrease = 12;
                break;
            default:
                fishingCatch = "garbage";
                expIncrease = 8;
                break;
        }

        Global.FireAndForget(ModifyItem(dbUser, fishingCatch, +1));

        int foundPet = await PetEngine.RollForPet(dbUser);

        if (foundPet > 0)
            expIncrease = (expIncrease * 5) / 2;

        int damage = 3 + Global.Randomize(4);
        poleStatus -= damage;
        Global.FireAndForget(ModifyItem(dbUser, "fishpole", -damage));

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser, FishColor);
        Global.AddTitle(container, "### 🎣 Fishing!");
        container.WithTextDisplay($"**You caught**\n{GetItemName(fishingCatch)}");
        container.WithTextDisplay($"**Rod durability**\n{DurabilityBar(poleStatus)} {poleStatus}%");

        if (foundPet > 0)
            container.WithTextDisplay($"**✨ You found a pet!**\nA friendly {PetEngine.PetNames(foundPet)} chased you about while you fished. It's been added to your pets.");

        if (poleStatus <= 0)
            container.WithTextDisplay($"**🎣 {GetItemName("fishpole")} destroyed**\nYour rod snapped. Pick up a new one at the shop.");

        Global.AddFooter(container, $"{dbUser.AddExp(expIncrease)}");

        ActionRowBuilder buttonRow = new();
        AddStandardButtons(ref buttonRow, except: "fish");
        if (foundPet > 0)
            buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary, emote: new Emoji("🐾"));
        container.WithActionRow(buttonRow);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await interaction.RespondAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    public static async Task ShowInventoryFunc(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);

        await interaction.DeferAsync();

        int dabloons = await GetItem(dbUser, "dabloons");
        int seeds = await GetItem(dbUser, "seedbag");

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser);
        Global.AddTitle(container, "### 🎒 Inventory");

        string tools = await ListItems(dbUser, ["fishpole", "farmtools"]);
        if (!string.IsNullOrWhiteSpace(tools))
            container.WithTextDisplay(tools.TrimEnd());

        string harvest = await ListItems(dbUser, ["tomato", "carrot", "potato"]);
        string catchItems = await ListItems(dbUser, ["fish", "uncommonfish", "rarefish", "shrimp"]);

        if (!string.IsNullOrWhiteSpace(harvest) || !string.IsNullOrWhiteSpace(catchItems))
        {
            string items = "";
            if (!string.IsNullOrWhiteSpace(harvest)) items += harvest;
            if (!string.IsNullOrWhiteSpace(catchItems)) items += catchItems;
            container.WithTextDisplay(items.TrimEnd());
        }

        string misc = await ListItems(dbUser, ["garbage"]);
        if (!string.IsNullOrWhiteSpace(misc))
            container.WithTextDisplay(misc.TrimEnd());

        Global.AddFooter(container, $"🐾 {dabloons} dabloons  •  🌱 {seeds} seeds  •  ✨ {dbUser.Exp} exp");

        ActionRowBuilder buttonRow = new();
        AddStandardButtons(ref buttonRow, except: "inventory");
        buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary, emote: new Emoji("🐾"));
        container.WithActionRow(buttonRow);

        Pet? pet = await PetEngine.GetUserPet(dbUser);

        if (pet is not null)
        {
            ActionRowBuilder petRow = new();
            petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
            petRow.WithButton(label: $"{pet.GetEmoji()} information", customId: "pet_view", style: ButtonStyle.Secondary);
            container.WithActionRow(petRow);
        }

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await interaction.FollowupAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    public static async Task ShowShopFunc(SocketInteraction interaction, SocketUser user)
    {
        var dbUser = await UserEngine.GetDbUser(user);
        if (!dbUser.IsValid()) return;

        int dabloons = await GetItem(dbUser, "dabloons");

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser, ShopColor);
        Global.AddTitle(container, "### 🛒 Shop");
        container.WithTextDisplay("Use the menus below to buy or sell items.");

        Global.AddFooter(container, $"🐾 {dabloons} dabloons");

        GetShopComponentsV2(container);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await interaction.RespondAsync(components: comps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
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
            buyMenu.AddOption(
                label: $"1 {GetItemName("seedbag")}", value: "seedbag:1",
                description: $"{ItemBuyChart["seedbag"]} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"5 {GetItemName("seedbag")}", value: "seedbag:5",
                description: $"{ItemBuyChart["seedbag"] * 5} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"10 {GetItemName("seedbag")}", value: "seedbag:10",
                description: $"{ItemBuyChart["seedbag"] * 10} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"1 {GetItemName("fishpole")}", value: "fishpole:1",
                description: $"{ItemBuyChart["fishpole"]} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"1 {GetItemName("farmtools")}", value: "farmtools:1",
                description: $"{ItemBuyChart["farmtools"]} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"1 {GetItemName("plots")}", value: "plots:1",
                description: $"{ItemBuyChart["plots"]} {GetItemName("dabloons")}");
        }
        else
        {
            buyMenu.AddOption(label: "Please wait...", value: "NULL");
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
            foreach (var item in ItemSaleChart.Keys)
            {
                sellMenu.AddOption(
                    label: $"{ItemSaleChart[item].amount} {GetItemName(item)}",
                    description: $"{ItemSaleChart[item].cost} {GetItemName("dabloons")}",
                    value: item
                );
            }
        }
        else
        {
            sellMenu.AddOption(label: "Please wait...", value: "NULL");
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
            foreach (var item in ItemSaleChart.Keys)
            {
                sellAllMenu.AddOption(
                    label: GetItemName(item),
                    description: $"{ItemSaleChart[item].cost} {GetItemName("dabloons")} per every {ItemSaleChart[item].amount}",
                    value: item
                );
            }
        }
        else
        {
            sellAllMenu.AddOption(label: "Please wait...", value: "NULL");
        }
        sellAllMenu.MaxValues = 1;
        ActionRowBuilder buttonRow = new();

        AddStandardButtons(ref buttonRow, except: "shop");

        return
            new ComponentBuilder()
                .WithSelectMenu(buyMenu)
                .WithSelectMenu(sellMenu)
                .WithSelectMenu(sellAllMenu)
                .AddRow(buttonRow);
    }

    private static void GetShopComponentsV2(ContainerBuilder container, bool empty = false)
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
            buyMenu.AddOption(
                label: $"1 {GetItemName("seedbag")}", value: "seedbag:1",
                description: $"{ItemBuyChart["seedbag"]} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"5 {GetItemName("seedbag")}", value: "seedbag:5",
                description: $"{ItemBuyChart["seedbag"] * 5} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"10 {GetItemName("seedbag")}", value: "seedbag:10",
                description: $"{ItemBuyChart["seedbag"] * 10} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"1 {GetItemName("fishpole")}", value: "fishpole:1",
                description: $"{ItemBuyChart["fishpole"]} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"1 {GetItemName("farmtools")}", value: "farmtools:1",
                description: $"{ItemBuyChart["farmtools"]} {GetItemName("dabloons")}");
            buyMenu.AddOption(
                label: $"1 {GetItemName("plots")}", value: "plots:1",
                description: $"{ItemBuyChart["plots"]} {GetItemName("dabloons")}");
        }
        else
        {
            buyMenu.AddOption(label: "Please wait...", value: "NULL");
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
            foreach (var item in ItemSaleChart.Keys)
            {
                sellMenu.AddOption(
                    label: $"{ItemSaleChart[item].amount} {GetItemName(item)}",
                    description: $"{ItemSaleChart[item].cost} {GetItemName("dabloons")}",
                    value: item
                );
            }
        }
        else
        {
            sellMenu.AddOption(label: "Please wait...", value: "NULL");
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
            foreach (var item in ItemSaleChart.Keys)
            {
                sellAllMenu.AddOption(
                    label: GetItemName(item),
                    description: $"{ItemSaleChart[item].cost} {GetItemName("dabloons")} per every {ItemSaleChart[item].amount}",
                    value: item
                );
            }
        }
        else
        {
            sellAllMenu.AddOption(label: "Please wait...", value: "NULL");
        }
        sellAllMenu.MaxValues = 1;

        ActionRowBuilder buyRow = new();
        buyRow.WithSelectMenu(buyMenu);
        container.WithActionRow(buyRow);

        ActionRowBuilder sellRow = new();
        sellRow.WithSelectMenu(sellMenu);
        container.WithActionRow(sellRow);

        ActionRowBuilder sellAllRow = new();
        sellAllRow.WithSelectMenu(sellAllMenu);
        container.WithActionRow(sellAllRow);

        ActionRowBuilder buttonRow = new();
        AddStandardButtons(ref buttonRow, except: "shop");
        container.WithActionRow(buttonRow);
    }

    public static void AddStandardButtons(ref ActionRowBuilder buttonRow, string except = "none")
    {
        if (except != "fish")
            buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary, emote: new Emoji("🎣"));
        if (except != "farm")
            buttonRow.WithButton(label: "Farm", customId: "farm", style: ButtonStyle.Primary, emote: new Emoji("🌾"));
        if (except != "shop")
            buttonRow.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Secondary, emote: new Emoji("🛒"));
        if (except != "inventory")
            buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary, emote: new Emoji("🎒"));
    }

}
