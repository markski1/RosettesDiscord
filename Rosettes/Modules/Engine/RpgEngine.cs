using Discord.Interactions;
using Discord;
using System.Xml.Linq;
using Discord.WebSocket;
using Rosettes.Core;

namespace Rosettes.Modules.Engine
{
    public static class RpgEngine
    {
        public static string GetItemName(string choice)
        {
            return choice switch
            {
                "fish" => "🐡 Common fish",
                "uncommonfish" => "🐟 Uncommon fish",
                "rarefish" => "🐠 Rare fish",
                "shrimp" => "🦐 Shrimp",
                "sushi" => "🍣 Sushi",
                "rice" => "🍙 Rice",
                "shrimprice" => "🍚 Shrimp Fried Rice",
                "garbage" => "🗑 Garbage",
                "dabloons" => "🐾 Dabloons",
                _ => "invalid item"
            };
        }

        public static async void ModifyItem(User dbUser, string choice, int amount)
        {
            await UserEngine._interface.ModifyInventoryItem(dbUser, choice, amount);
        }

        public static async Task<int> GetItem(User dbUser, string name)
        {
            return await UserEngine._interface.FetchInventoryItem(dbUser, name);
        }

        public static async Task<string> CanuseRPGCommand(SocketInteractionContext context)
        {
            if (context.Guild is null)
            {
                return "RPG Commands do not work in direct messages.";
            }
            var dbGuild = await GuildEngine.GetDBGuild(context.Guild);
            if (!dbGuild.AllowsRPG())
            {
                return "This guild does not allow RPG commands.";
            }
            if (dbGuild.LogChannel != 0 && dbGuild.LogChannel != context.Channel.Id)
            {
                return "RPG commands are not allowed in this channel, please use the RPG/bot channel.";
            }
            return "yes";
        }

        public static async Task CraftAction(SocketMessageComponent component)
        {
            var dbUser = await UserEngine.GetDBUser(component.User);
            EmbedBuilder embed = Global.MakeRosettesEmbed(component.User);
            string choice = component.Data.Values.Last();

            string ingredientsOrSuccess = await RpgEngine.HasIngredients(dbUser, choice);
            if (ingredientsOrSuccess != "success")
            {
                embed.Title = "Not enough materials.";
                embed.Description = $"You need at least {ingredientsOrSuccess} to make {RpgEngine.GetItemName(choice)}.";
            }
            else
            {
                embed.Title = "Crafted new item.";

                ingredientsOrSuccess = RpgEngine.MakeItem(dbUser, choice);

                embed.AddField("Spent:", ingredientsOrSuccess);
                embed.AddField("Made:", RpgEngine.GetItemName(choice));

                embed.Footer = new EmbedFooterBuilder()
                {
                    Text = $"added to inventory."
                };
            }

            await component.RespondAsync(embed: embed.Build());
        }

        public static async Task<string> HasIngredients(User dbUser, string item)
        {
            switch (item)
            {
                case "sushi":
                    if (await GetItem(dbUser, "fish") >= 2 && await GetItem(dbUser, "uncommonfish") >= 1 && await GetItem(dbUser, "rice") >= 1)
                    {
                        return "success";
                    }
                    break;
                case "shrimprice":
                    if (await GetItem(dbUser, "shrimp") >= 2 && await GetItem(dbUser, "rice") >= 1)
                    {
                        return "success";
                    }
                    break;
            }
            return GetCraftingCost(item);
        }

        public static string MakeItem(User dbUser, string item)
        {
            switch (item)
            {
                case "sushi":
                    ModifyItem(dbUser, "fish", -2);
                    ModifyItem(dbUser, "uncommonfish", -1);
                    ModifyItem(dbUser, "rice", -1);
                    ModifyItem(dbUser, "sushi", +1);
                    break;
                case "shrimprice":
                    ModifyItem(dbUser, "shrimp", -2);
                    ModifyItem(dbUser, "rice", -1);
                    ModifyItem(dbUser, "shrimprice", +1);
                    break;
            }
            return GetCraftingCost(item);
        }

        public static string GetCraftingCost(string item)
        {
            switch (item)
            {
                case "sushi":
                    return $"2 {GetItemName("fish")}, 1 {GetItemName("uncommonfish")} and 1 {GetItemName("rice")}";
                case "shrimprice":
                    return $"2 {GetItemName("shrimp")} and 1 {GetItemName("rice")}";
                default:
                    break;
            }
            return "error";
        }

        public static bool IsValidMakeChoice(string choice)
        {
            string[] choices = { "sushi", "shrimprice" };
            return choices.Contains(choice);
        }

        public static bool IsValidGiveChoice(string choice)
        {
            string[] choices = { "sushi", "shrimprice" };
            return choices.Contains(choice);
        }

        public static bool IsValidUseChoice(string choice)
        {
            string[] choices = { "sushi", "shrimprice", "garbage" };
            return choices.Contains(choice);
        }

        public static async Task ShopAction(SocketMessageComponent component)
        {
            var dbUser = await UserEngine.GetDBUser(component.User);

            var embed = Global.MakeRosettesEmbed(component.User);

            switch (component.Data.CustomId)
            {
                case "buy":
                    switch (component.Data.Values.Last())
                    {
                        case "buy1":
                            if (await GetItem(dbUser, "dabloons") >= 5)
                            {
                                ModifyItem(dbUser, "dabloons", -5);
                                ModifyItem(dbUser, "rice", +2);
                                embed.Description = $"You have purchased 2 {GetItemName("rice")} for 5 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have enough {GetItemName("dabloons")}";
                            }
                            break;
                        case "buy2":
                            if (await GetItem(dbUser, "dabloons") >= 2)
                            {
                                ModifyItem(dbUser, "dabloons", -2);
                                ModifyItem(dbUser, "fish", +1);
                                embed.Description = $"You have purchased 1 {GetItemName("fish")} for 2 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have enough {GetItemName("dabloons")}";
                            }
                            break;
                        case "buy3":
                            embed.Title = "Purchase";
                            if (await GetItem(dbUser, "dabloons") >= 5)
                            {
                                ModifyItem(dbUser, "dabloons", -5);
                                ModifyItem(dbUser, "uncommonfish", +1);
                                embed.Description = $"You have purchased 1 {GetItemName("uncommonfish")} for 5 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have enough {GetItemName("dabloons")}";
                            }
                            break;
                    }
                    break;

                case "sell":
                    embed.Title = "Sale";
                    switch (component.Data.Values.Last())
                    {
                        case "sell1":
                            if (await GetItem(dbUser, "rarefish") >= 1)
                            {
                                ModifyItem(dbUser, "rarefish", -1);
                                ModifyItem(dbUser, "dabloons", +5);
                                embed.Description = $"You have sold 1 {GetItemName("rarefish")} for 5 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have enough {GetItemName("rarefish")}";
                            }
                            break;
                        case "sell2":
                            if (await GetItem(dbUser, "garbage") >= 5)
                            {
                                ModifyItem(dbUser, "garbage", -5);
                                ModifyItem(dbUser, "dabloons", +5);
                                embed.Description = $"You have sold 5 {GetItemName("garbage")} for 5 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have enough {GetItemName("garbage")}";
                            }
                            break;
                    }
                    break;
            }

            await component.RespondAsync(text: component.Data.Value, embed: embed.Build());
        }

        public static async Task<string> ListItems(User user, List<string> items)
        {
            string list = "";

            foreach (var item in items)
            {
                int amount = await GetItem(user, item);

                if (amount != 0)
                {
                    list += $"{RpgEngine.GetItemName(item)}: {amount}\n";
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
            var dbUser = await UserEngine.GetDBUser(user);
            if (!dbUser.CanFish())
            {
                await interaction.RespondAsync("You can only fish every 60 minutes.");
                return;
            }
            EmbedBuilder embed = Global.MakeRosettesEmbed(user);
            embed.Title = "Fishing! 🎣";
            EmbedFieldBuilder fishField = new()
            {
                Name = "Catching...",
                Value = "`[|||       ]`"
            };
            embed.AddField(fishField);
            await interaction.RespondAsync(embed: embed.Build());

            await Task.Delay(250);

            fishField.Value = "`[||||||    ]`";
            await interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());

            await Task.Delay(250);

            fishField.Value = "`[||||||||| ]`";
            await interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());

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

            await interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary);
            buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);
            buttonRow.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.ModifyOriginalResponseAsync(msg => msg.Components = comps.Build());
        }

        public static async Task ShowInventoryFunc(SocketInteraction interaction, IUser user)
        {
            EmbedBuilder embed = Global.MakeRosettesEmbed(user);
            embed.Title = $"Inventory";
            embed.Description = "Loading inventory...";

            await interaction.RespondAsync(embed: embed.Build());

            User dbUser = await UserEngine.GetDBUser(user);

            List<string> fieldsToList = new();

            EmbedFooterBuilder footer = new() { Text = $"Wallet: {await RpgEngine.GetItem(dbUser, "dabloons")} {RpgEngine.GetItemName("dabloons")}" };

            embed.Footer = footer;

            fieldsToList.Add("garbage");
            fieldsToList.Add("rice");

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
            fieldsToList.Add("sushi");
            fieldsToList.Add("shrimprice");

            embed.AddField(
                $"Finished Goods",
                await ListItems(dbUser, fieldsToList),
                true
            );

            embed.Description = null;

            await interaction.ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            SelectMenuBuilder craftMenu = new()
            {
                Placeholder = "Craft menu",
                CustomId = "make"
            };
            craftMenu.AddOption(label: RpgEngine.GetItemName("sushi"), description: RpgEngine.GetCraftingCost("sushi"), value: "sushi");
            craftMenu.AddOption(label: RpgEngine.GetItemName("shrimprice"), description: RpgEngine.GetCraftingCost("shrimprice"), value: "shrimprice");
            craftMenu.MaxValues = 1;

            comps.WithSelectMenu(craftMenu);
            buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary);
            buttonRow.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.ModifyOriginalResponseAsync(msg => msg.Components = comps.Build());
        }

        public static async Task ShowShopFunc(SocketInteraction interaction, SocketUser user)
        {
            var dbUser = await UserEngine.GetDBUser(user);
            if (dbUser is null) return;
            EmbedBuilder embed = Global.MakeRosettesEmbed();
            embed.Title = "Rosettes shop!";
            embed.Description = $"The shop allows for buying and selling items for doubloons.";

            embed.Footer = new EmbedFooterBuilder() { Text = $"[{user.Username}] has: {await RpgEngine.GetItem(dbUser, "dabloons")} {RpgEngine.GetItemName("dabloons")}" };

            var comps = new ComponentBuilder();

            SelectMenuBuilder buyMenu = new()
            {
                Placeholder = "Buy...",
                CustomId = "buy"
            };
            buyMenu.AddOption(label: $"2 {RpgEngine.GetItemName("rice")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "buy1");
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("fish")}", description: $"2 {RpgEngine.GetItemName("dabloons")}", value: "buy2");
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("uncommonfish")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "buy3");
            buyMenu.MaxValues = 1;

            SelectMenuBuilder sellMenu = new()
            {
                Placeholder = "Sell...",
                CustomId = "sell"
            };
            sellMenu.AddOption(label: $"1 {RpgEngine.GetItemName("rarefish")}]", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell1");
            sellMenu.AddOption(label: $"5 {RpgEngine.GetItemName("garbage")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell2");
            sellMenu.MaxValues = 1;

            comps.WithSelectMenu(buyMenu, 0);
            comps.WithSelectMenu(sellMenu, 0);

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }
    }
}