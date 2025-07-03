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
    public static readonly Dictionary<string, (string fullName, bool can_give, bool can_pet_eat)> InventoryItems = new()
    {
        // db_name / (name / can_give / can_pet_eat)
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

    public static async void ModifyItem(User dbUser, string choice, int amount)
    {
        await FarmRepository.ModifyInventoryItem(dbUser, choice, amount);
    }

    private static async void SetItem(User dbUser, string choice, int newValue)
    {
        await FarmRepository.SetInventoryItem(dbUser, choice, newValue);
    }
    
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
        
        var parts = component.Data.Values.Last().Split(':');
        string text = "";
        
        switch (component.Data.CustomId)
        {
            // A purchase is composed by the name of the item purchased, and its amount (i.e., seedbag:5, fishpole:1)
            case "buy" when parts.Length == 2:
            {
                string item = parts[0];
                int amount = int.Parse(parts[1]);
                
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
                        ModifyItem(dbUser, "dabloons", -10);
                        SetItem(dbUser, "fishpole", 100);
                        break;
                    case "farmtools":
                        if (await GetItem(dbUser, "farmtools") >= 30)
                        {
                            text = $"Your current {GetItemName("farmtools")} are still in good shape.";
                            break;
                        }
                        ModifyItem(dbUser, "dabloons", -15);
                        SetItem(dbUser, "farmtools", 100);
                        break;
                    case "plots":
                        if (await GetItem(dbUser, "plots") >= 3)
                        {
                            text = "For the time being, you may not own more than 3 plots of land.";
                            break;
                        }
                        ModifyItem(dbUser, "dabloons", -200);
                        ModifyItem(dbUser, "plots", +1);
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

    private static string ItemBuy(User dbUser, string boughtItem, int amount, int cost, bool setType = false)
    {
        ModifyItem(dbUser, "dabloons", -cost);
        if (setType)
        {
            SetItem(dbUser, boughtItem, amount);
        }
        else
        {
            ModifyItem(dbUser, boughtItem, +amount);
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

            ModifyItem(dbUser, selling, -totalSold);
            ModifyItem(dbUser, "dabloons", +totalEarned);

            return $"You have sold {totalSold} {GetItemName(selling)} for {totalEarned} {GetItemName("dabloons")}";
        }

        ModifyItem(dbUser, selling, -amount);
        ModifyItem(dbUser, "dabloons", +cost);
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

        embed.Footer = new EmbedFooterBuilder
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

        List<string> fieldsToList = [];

        EmbedFooterBuilder footer = new() { Text = $"{await GetItem(dbUser, "dabloons")} {GetItemName("dabloons")} | {await GetItem(dbUser, "seedbag")} {GetItemName("seedbag")}\n{dbUser.Exp} experience" };

        embed.Footer = footer;

        fieldsToList.Add("garbage");
        fieldsToList.Add("fishpole");
        fieldsToList.Add("farmtools");

        embed.AddField(
            "Items",
            await ListItems(dbUser, fieldsToList)
        );

        fieldsToList.Clear();
        fieldsToList.Add("fish");
        fieldsToList.Add("uncommonfish");
        fieldsToList.Add("rarefish");
        fieldsToList.Add("shrimp");

        embed.AddField(
            "Catch",
            await ListItems(dbUser, fieldsToList),
            true
        );

        fieldsToList.Clear();
        fieldsToList.Add("tomato");
        fieldsToList.Add("carrot");
        fieldsToList.Add("potato");

        embed.AddField(
            "Harvest",
            await ListItems(dbUser, fieldsToList),
            true
        );

        embed.Description = null;

        ComponentBuilder comps = new();

        ActionRowBuilder buttonRow = new();
        AddStandardButtons(ref buttonRow, except: "inventory");
        buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);

        comps.AddRow(buttonRow);

        Pet? pet = await PetEngine.GetUserPet(dbUser);

        if (pet is not null)
        {
            ActionRowBuilder petRow = new();
            petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
            petRow.WithButton(label: $"{pet.GetEmoji()} information", customId: "pet_view", style: ButtonStyle.Secondary);
            comps.AddRow(petRow);
        }

        await interaction.FollowupAsync(embed: embed.Build(), components: comps.Build());
    }

    public static async Task ShowShopFunc(SocketInteraction interaction, SocketUser user)
    {
        var dbUser = await UserEngine.GetDbUser(user);
        if (!dbUser.IsValid()) return;

        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
        embed.Description = "The shop allows for buying and selling items for dabloons.";

        embed.Footer = new EmbedFooterBuilder { Text = $"You have: {await GetItem(dbUser, "dabloons")} {GetItemName("dabloons")}" };

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

    public static void AddStandardButtons(ref ActionRowBuilder buttonRow, string except = "none")
    {
        if (except != "fish") buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary);
        if (except != "farm") buttonRow.WithButton(label: "Farm", customId: "farm", style: ButtonStyle.Primary);
        if (except != "shop") buttonRow.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Secondary);
        if (except != "inventory") buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);
    }

}