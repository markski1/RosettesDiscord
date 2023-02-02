using Discord.Interactions;
using Discord;
using System.Xml.Linq;
using Discord.WebSocket;
using Rosettes.Core;
using System.Text;

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

        public static async void ModifyStrItem(User dbUser, string choice, string newValue)
        {
            await UserEngine._interface.ModifyStrInventoryItem(dbUser, choice, newValue);
        }

        public static async Task<int> GetItem(User dbUser, string name)
        {
            return await UserEngine._interface.FetchInventoryItem(dbUser, name);
        }

        public static async Task<string> GetStrItem(User dbUser, string name)
        {
            return await UserEngine._interface.FetchInventoryStringItem(dbUser, name);
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
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
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

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

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

        public static async Task SetDefaultPet(SocketMessageComponent component)
        {
            var dbUser = await UserEngine.GetDBUser(component.User);

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            int petRequested = Int32.Parse(component.Data.Values.Last());

            if (petRequested < 1 || petRequested > 10)
            {
                dbUser.SetPet(0);
                embed.Title = "Main pet removed.";
                embed.Description = "You no longer have a main pet.";
            }
            else if (await HasPet(dbUser, petRequested))
            {
                dbUser.SetPet(petRequested);
                embed.Title = "Main pet set.";
                embed.Description = $"Your main pet is now your {PetNames(petRequested)}";
            }
            else
            {
                embed.Title = "Main pet not set.";
                embed.Description = $"You do not have a {PetNames(petRequested)}";
            }

            await component.RespondAsync(embed: embed.Build());
        }

        public static async Task<bool> HasPet(User dbUser, int id)
        {
            // make zero-indexed
            id--;
            string pets = await GetStrItem(dbUser, "pets");

            return (pets != null && pets[id] == '1');
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

        public static string PetNames(int id)
        {
            return id switch
            {
                1 => "🐕 Dog",
                2 => "🦊 Fox",
                3 => "🐈 Cat",
                4 => "🐐 Goat",
                5 => "🐇 Rabbit",
                6 => "🦇 Bat",
                7 => "🐦 Bird",
                8 => "🦎 Lizard",
                9 => "🐹 Hamster",
                10 => "🐸 Frog",
                11 => "🦝 Raccoon",
                12 => "🐼 Panda",
                13 => "🐁 Mice",
                14 => "🐊 Crocodile",
                15 => "🐢 Turtle",
                16 => "🦦 Otter",
                17 => "🦜 Parrot",
                18 => "🦨 Skunk",
                19 => "🐿 Chipmunk",
                _ => "? Invalid Pet"
            };
        }

        public static string PetEmojis(int id)
        {
            return id switch
            {
                1 => "🐕",
                2 => "🦊",
                3 => "🐈",
                4 => "🐐",
                5 => "🐇",
                6 => "🦇",
                7 => "🐦",
                8 => "🦎",
                9 => "🐹",
                10 => "🐸",
                11 => "🦝",
                12 => "🐼",
                13 => "🐁",
                14 => "🐊",
                15 => "🐢",
                16 => "🦦",
                17 => "🦜",
                18 => "🦨",
                19 => "🐿",
                _ => "?"
            };
        }

        public static async Task<int> RollForPet(User dbUser)
        {
            Random rand = new();

            if (rand.Next(25) == 0)
            {
                int pet;
                int attempts = 0;
                while (true)
                {
                    pet = rand.Next(19);
                    if (await HasPet(dbUser, pet) == false) break;
                    
                    // if after 5 attempts there's only repeated pets, don't get a pet.
                    attempts++;
                    if (attempts == 5) return 0;
                }

                string userPets = await GetStrItem(dbUser, "pets");

                char[] petsAsChars = userPets.ToCharArray();

                petsAsChars[pet] = '1';

                ModifyStrItem(dbUser, "pets", new string(petsAsChars));

                return pet + 1;
            }

            return 0;
        }

        // main funcs

        public static async Task CatchFishFunc(SocketInteraction interaction, IUser user)
        {
            var dbUser = await UserEngine.GetDBUser(user);
            
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary);
            buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);
            buttonRow.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            if (!dbUser.CanFish())
            {
                embed.Title = "Can't fish yet.";
                embed.Description = $"You may fish again <t:{dbUser.LastFished}:R>";

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
                return;
            }

            embed.Title = "Fishing! 🎣";
            EmbedFieldBuilder fishField = new()
            {
                Name = "Catching...",
                Value = "`[||||      ]`"
            };
            embed.AddField(fishField);
            await interaction.RespondAsync(embed: embed.Build());

            await Task.Delay(250);

            fishField.Value = "`[||||||||  ]`";
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

            int foundPet = await RollForPet(dbUser);

            if (foundPet > 0)
            {
                embed.AddField("You found a pet!", $"While fishing, you found a friendly {PetNames(foundPet)}, who chased you about. It has been added to your pets.");
            }

            await interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
            await interaction.ModifyOriginalResponseAsync(msg => msg.Components = comps.Build());
        }

        public static async Task ShowInventoryFunc(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Inventory";
            embed.Description = "Loading inventory...";

            await interaction.RespondAsync(embed: embed.Build());


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
            buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.ModifyOriginalResponseAsync(msg => msg.Components = comps.Build());
        }

        public static async Task ShowPets(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Pets";
            embed.Description = "Loading pets...";

            await interaction.RespondAsync(embed: embed.Build());


            string petString = "";
            List<int> petList = new();

            string petsOwned = await GetStrItem(dbUser, "pets");

            int count = 1;

            foreach(char pet in petsOwned)
            {
                if (pet == '1')
                {
                    petString += $"{PetNames(count)}\n";
                    petList.Add(count);
                }
                count++;
            }

            if (petString == "")
            {
                petString = "None. You can randomly find pets during activities such as fishing.";
            }

            embed.AddField("Pets in ownership:", petString);

            embed.Description = null;

            await interaction.ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            SelectMenuBuilder craftMenu = new()
            {
                Placeholder = "Set default pet",
                CustomId = "defaultPet"
            };
            craftMenu.AddOption(label: "None", value: "0");
            foreach (int pet in petList)
            {
                craftMenu.AddOption(label: PetNames(pet), value: $"{pet}");
            }

            craftMenu.MaxValues = 1;

            comps.WithSelectMenu(craftMenu);
            buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary);
            buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.ModifyOriginalResponseAsync(msg => msg.Components = comps.Build());
        }

        public static async Task ShowShopFunc(SocketInteraction interaction, SocketUser user)
        {
            var dbUser = await UserEngine.GetDBUser(user);
            if (dbUser is null) return;

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
            embed.Title = "Rosettes shop!";
            embed.Description = $"The shop allows for buying and selling items for dabloons.";

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
            sellMenu.AddOption(label: $"1 {RpgEngine.GetItemName("rarefish")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell1");
            sellMenu.AddOption(label: $"5 {RpgEngine.GetItemName("garbage")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell2");
            sellMenu.MaxValues = 1;

            comps.WithSelectMenu(buyMenu, 0);
            comps.WithSelectMenu(sellMenu, 0);


            ActionRowBuilder buttonRow = new();

            buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary);
            buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }
    }
}