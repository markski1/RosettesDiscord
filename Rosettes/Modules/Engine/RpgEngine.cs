using Discord.Interactions;
using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Modules.Engine.RPG;

namespace Rosettes.Modules.Engine
{
    public static class RpgEngine
    {
        public static readonly RpgRepository _interface = new();

        public static string GetItemName(string choice)
        {
            return choice switch
            {
                "fish" => "🐡 Common fish",
                "uncommonfish" => "🐟 Uncommon fish",
                "rarefish" => "🐠 Rare fish",
                "shrimp" => "🦐 Shrimp",
                "dabloons" => "🐾 Dabloons",
                "garbage" => "🗑 Garbage",
                "tomato" => "🍅 Tomato",
                "carrot" => "🥕 Carrot",
                "potato" => "🥔 Potato",
                "seedbag" => "🌱 Seed bag",
                "fishpole" => "🎣 Fishing pole",
                "farmtools" => "🧰 Farming tools",
                _ => "invalid item"
            };
        }

        public static async void ModifyItem(User dbUser, string choice, int amount)
        {
            await _interface.ModifyInventoryItem(dbUser, choice, amount);
        }

        public static async void SetItem(User dbUser, string choice, int newValue)
        {
            await _interface.SetInventoryItem(dbUser, choice, newValue);
        }

        public static async void ModifyStrItem(User dbUser, string choice, string newValue)
        {
            await _interface.ModifyStrInventoryItem(dbUser, choice, newValue);
        }

        public static async Task<int> GetItem(User dbUser, string name)
        {
            return await _interface.FetchInventoryItem(dbUser, name);
        }

        public static async Task<string> GetStrItem(User dbUser, string name)
        {
            return await _interface.FetchInventoryStringItem(dbUser, name);
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

        public static bool IsValidGiveChoice(string choice)
        {
            
            string[] choices = 
                {
                    "fish",
                    "uncommonfish",
                    "rarefish",
                    "shrimp",
                    "dabloons",
                    "garbage",
                    "tomato",
                    "carrot",
                    "potato",
                    "seedbag"
                };
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
                                ModifyItem(dbUser, "seedbag", +1);
                                embed.Description = $"You have purchased {GetItemName("seedbag")} for 5 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have 5 {GetItemName("dabloons")}";
                            }
                            break;
                        case "buy2":
                            if (await GetItem(dbUser, "dabloons") >= 5)
                            {
                                if (await GetItem(dbUser, "fishpole") >= 25)
                                {
                                    embed.Description = $"Your current {GetItemName("fishpole")} are still in good shape.";
                                }
                                else
                                {
                                    ModifyItem(dbUser, "dabloons", -5);
                                    SetItem(dbUser, "fishpole", 100);
                                    embed.Description = $"You have purchased {GetItemName("fishpole")} for 5 {GetItemName("dabloons")}";
                                }
                            }
                            else
                            {
                                embed.Description = $"You don't have 5 {GetItemName("dabloons")}";
                            }
                            break;
                        case "buy3":
                            embed.Title = "Purchase";
                            if (await GetItem(dbUser, "dabloons") >= 10)
                            {
                                if (await GetItem(dbUser, "farmtools") >= 25)
                                {
                                    embed.Description = $"Your current {GetItemName("farmtools")} are still in good shape.";
                                }
                                else
                                {
                                    ModifyItem(dbUser, "dabloons", -10);
                                    SetItem(dbUser, "farmtools", 100);
                                    embed.Description = $"You have purchased {GetItemName("farmtools")} for 10 {GetItemName("dabloons")}";
                                }
                            }
                            else
                            {
                                embed.Description = $"You don't have 10 {GetItemName("dabloons")}";
                            }
                            break;
                        case "buy4":
                            embed.Title = "Purchase";
                            if (await GetItem(dbUser, "dabloons") >= 200)
                            {
                                if (await GetItem(dbUser, "plots") >= 3)
                                {
                                    embed.Description = $"For the time being, you may not own more than 3 plots of land!";
                                }
                                else
                                {
                                    ModifyItem(dbUser, "dabloons", -200);
                                    SetItem(dbUser, "plots", 100);
                                    embed.Description = $"You have purchased a plot of land for 200 {GetItemName("dabloons")}";
                                }
                            }
                            else
                            {
                                embed.Description = $"You don't have 200 {GetItemName("dabloons")}";
                            }
                            break;
                    }
                    break;

                case "sell":
                    embed.Title = "Sale";
                    switch (component.Data.Values.Last())
                    {
                        case "sell1":
                            if (await GetItem(dbUser, "fish") >= 5)
                            {
                                ModifyItem(dbUser, "fish", -5);
                                ModifyItem(dbUser, "dabloons", +5);
                                embed.Description = $"You have sold 5 {GetItemName("fish")} for 3 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have 5 {GetItemName("fish")}";
                            }
                            break;
                        case "sell2":
                            if (await GetItem(dbUser, "uncommonfish") >= 5)
                            {
                                ModifyItem(dbUser, "uncommonfishfish", -5);
                                ModifyItem(dbUser, "dabloons", +5);
                                embed.Description = $"You have sold 5 {GetItemName("uncommonfish")} for 3 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have 5 {GetItemName("uncommonfish")}";
                            }
                            break;
                        case "sell3":
                            if (await GetItem(dbUser, "rarefish") >= 1)
                            {
                                ModifyItem(dbUser, "rarefish", -1);
                                ModifyItem(dbUser, "dabloons", +5);
                                embed.Description = $"You have sold 1 {GetItemName("rarefish")} for 5 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have a {GetItemName("rarefish")}";
                            }
                            break;
                        case "sell4":
                            if (await GetItem(dbUser, "tomato") >= 10)
                            {
                                ModifyItem(dbUser, "tomato", -10);
                                ModifyItem(dbUser, "dabloons", +5);
                                embed.Description = $"You have sold 10 {GetItemName("tomato")} for 5 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have 10 {GetItemName("tomato")}";
                            }
                            break;
                        case "sell5":
                            if (await GetItem(dbUser, "carrot") >= 10)
                            {
                                ModifyItem(dbUser, "carrot", -10);
                                ModifyItem(dbUser, "dabloons", +3);
                                embed.Description = $"You have sold 10 {GetItemName("carrot")} for 4 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have 10 {GetItemName("carrot")}";
                            }
                            break;
                        case "sell6":
                            if (await GetItem(dbUser, "potato") >= 10)
                            {
                                ModifyItem(dbUser, "potato", -10);
                                ModifyItem(dbUser, "dabloons", +3);
                                embed.Description = $"You have sold 10 {GetItemName("potato")} for 3 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have 10 {GetItemName("potato")}";
                            }
                            break;
                        case "sell7":
                            if (await GetItem(dbUser, "garbage") >= 5)
                            {
                                ModifyItem(dbUser, "garbage", -5);
                                ModifyItem(dbUser, "dabloons", +2);
                                embed.Description = $"You have sold 5 {GetItemName("garbage")} for 2 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have 5 {GetItemName("garbage")}";
                            }
                            break;
                    }
                    break;
            }

            await component.RespondAsync(text: component.Data.Value, embed: embed.Build());
            
            await component.Message.ModifyAsync(x => x.Components = new ComponentBuilder().Build());

            await component.Message.ModifyAsync(x => x.Components = GetShopComponents().Build());
        }

        public static async Task SetDefaultPet(SocketMessageComponent component)
        {
            var dbUser = await UserEngine.GetDBUser(component.User);

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            int petRequested = Int32.Parse(component.Data.Values.Last());

            if (petRequested < 1 || petRequested > 23)
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

                if (item is "fishpole" or "farmtools")
                {
                    if (amount == 0)
                    {
                        list += $"{RpgEngine.GetItemName(item)}: broken\n";
                    }
                    else
                    {
                        list += $"{RpgEngine.GetItemName(item)}: `{amount}% status`\n";
                    }
                }
                else if (amount != 0)
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
                13 => "🐁 Mouse",
                14 => "🐊 Crocodile",
                15 => "🐢 Turtle",
                16 => "🦦 Otter",
                17 => "🦜 Parrot",
                18 => "🦨 Skunk",
                19 => "🐿 Chipmunk",
                20 => "🐼 Panda",
                21 => "🦉 Owl",
                22 => "🐺 Wolf",
                23 => "🦈 Shark",
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
                20 => "🐼",
                21 => "🦉",
                22 => "🐺",
                23 => "🦈",
                _ => "?"
            };
        }

        public static async Task<int> RollForPet(User dbUser)
        {
            Random rand = new();

            if (rand.Next(33) == 0)
            {
                int pet;
                int attempts = 0;
                while (true)
                {
                    pet = rand.Next(23);
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

            AddStandardButtons(ref buttonRow);

            comps.AddRow(buttonRow);

            if (!dbUser.CanFish())
            {
                embed.Title = "Can't fish yet.";
                embed.Description = $"You may fish again <t:{dbUser.LastFished}:R>";

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
                return;
            }

            var poleStatus = await GetItem(dbUser, "fishpole");

            if (poleStatus <= 0)
            {
                embed.Title = $"{GetItemName("fishpole")} broken!";
                embed.Description = $"Your {GetItemName("fishpole")} broke, you need a new one.";

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
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
            string fishingCatch;

            int expIncrease;

            switch (caught)
            {
                case (<= 40):
                    fishingCatch = "fish";
                    expIncrease = 10;
                    break;
                case (> 40 and <= 60):
                    fishingCatch = "uncommonfish";
                    expIncrease = 15;
                    break;
                case (> 60 and <= 65):
                    fishingCatch = "rarefish";
                    expIncrease = 18;
                    break;
                case (> 65 and < 85):
                    fishingCatch = "shrimp";
                    expIncrease = 12;
                    break;
                default:
                    fishingCatch = "garbage";
                    expIncrease = 8;
                    break;
            }

            fishField.Name = "You caught:";
            fishField.Value = RpgEngine.GetItemName(fishingCatch);

            RpgEngine.ModifyItem(dbUser, fishingCatch, +1);

            int foundPet = await RollForPet(dbUser);

            if (foundPet > 0)
            {
                embed.AddField("You found a pet!", $"While fishing, you found a friendly {PetNames(foundPet)}, who chased you about. It has been added to your pets.");
                buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);
                expIncrease *= 5;
                expIncrease /= 2;
            }

            int damage = 5 + rand.Next(8);

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

            await interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
            await interaction.ModifyOriginalResponseAsync(msg => msg.Components = comps.Build());
        }

        public static async Task ShowFarm(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Farm";

            List<Crop> fieldsToList = (await _interface.GetUserCrops(dbUser)).ToList();

            int plots = await _interface.FetchInventoryItem(dbUser, "plots");

            embed.Description = $"Your farm has {plots} plot{((plots != 1) ? 's' : null)} of land.";

            bool anyCanBePlanted = false;
            bool anyCanBeWatered = false;
            bool anyCanBeHarvested = false;

            int currentUnix = Global.CurrentUnix();

            for (int i = 1; i <= plots; i++)
            {
                Crop? currentCrop = fieldsToList.Find(x => x.plotId == i);
                if (currentCrop is null)
                {
                    embed.AddField($"Plot {i}", "There is nothing growing in this plot.", inline: true);
                    anyCanBePlanted = true;
                }
                else
                {
                    bool canBeHarvested = false;
                    bool canBeWatered = false;
                    if (currentCrop.unixGrowth < currentUnix)
                    {
                        canBeHarvested = true;
                        anyCanBeHarvested = true;
                    }
                    else if (currentCrop.unixNextWater < currentUnix)
                    {
                        canBeWatered = true;
                        anyCanBeWatered = true;
                    }

                    if (canBeWatered)
                    {
                        embed.AddField($"Plot {i}", $"There is {GetItemName(Farm.GetHarvest(currentCrop.cropType))} growing in this plot.\n It can be watered right now. It'll be ready to harvest <t:{currentCrop.unixGrowth}:R>");
                    }
                    else if (canBeHarvested)
                    {
                        embed.AddField($"Plot {i}", $"{GetItemName(Farm.GetHarvest(currentCrop.cropType))} has grown in this plot.\n It can be harvested right now.");
                    }
                    else
                    {
                        embed.AddField($"Plot {i}", $"There is {GetItemName(Farm.GetHarvest(currentCrop.cropType))} growing in this plot.\n It can be watered <t:{currentCrop.unixNextWater}:R>. It'll be ready to harvest <t:{currentCrop.unixGrowth}:R>");
                    }
                }
            }

            if (dbUser.GetFishTime() < Global.CurrentUnix())
            {
                embed.AddField("💦 Fishing Pond", "You may fish right now.");
            }
            else
            {
                embed.AddField("💦 Fishing Pond", $"You may fish again <t:{dbUser.GetFishTime()}:R>.");
            }

            EmbedFooterBuilder footer = new() { Text = $"TODO: Seeds" };

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            if (anyCanBeHarvested)
            {
                buttonRow.WithButton(label: "Harvest crops", customId: "crops_harvest", style: ButtonStyle.Success);
            }
            if (anyCanBePlanted)
            {
                buttonRow.WithButton(label: "Plant crops", customId: "crops_plant", style: ButtonStyle.Success);
            }
            if (anyCanBeWatered)
            {
                buttonRow.WithButton(label: "Water crops", customId: "crops_water", style: ButtonStyle.Success);
            }
            AddStandardButtons(ref buttonRow);

            comps.AddRow(buttonRow);

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }

        public static async Task PlantPlot(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Plant plot";

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            AddStandardButtons(ref buttonRow);

            comps.AddRow(buttonRow);

            var toolStatus = await GetItem(dbUser, "farmtools");

            if (toolStatus <= 0)
            {
                embed.Title = $"{GetItemName("farmtools")} broken!";
                embed.Description = $"Your {GetItemName("farmtools")} are broken, you need new ones.";

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
            }

            List<Crop> fieldsToList = (await _interface.GetUserCrops(dbUser)).ToList();

            int plots = await _interface.FetchInventoryItem(dbUser, "plots");

            if (fieldsToList.Count >= plots)
            {
                embed.Description = "You don't have any free plots to plant.";
                await interaction.RespondAsync(embed: embed.Build());
                return;
            }

            SocketUserMessage? originalMsg = GetOriginalMessage(interaction, dbUser);

            int seeds = await _interface.FetchInventoryItem(dbUser, "seedbag");

            if (seeds <= 0) {
                embed.Description = "You don't have any seeds, you may obtain them at the shop.";
                ComponentBuilder failComps = new();

                ActionRowBuilder failButtons = new();

                failButtons.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Primary);
                failButtons.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);

                failComps.AddRow(failButtons);
                await interaction.RespondAsync(embed: embed.Build(), components: failComps.Build());
                return;
            }

            List<int> occupiedPlots = new();

            foreach (var field in fieldsToList) {
                occupiedPlots.Add(field.plotId);
            }

            int plot_id = 1;
            while (occupiedPlots.Contains(plot_id)) plot_id++;

            if (plot_id > plots)
            {
                if (toolStatus <= 0)
                {
                    embed.Title = "No space to plant.";
                    embed.Description = "All your plots of land are currently occupied.";

                    await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
                }
            }

            Random rand = new();

            int roll = rand.Next(55);
            int type;

            if (roll < 10) type = 1; // tomatoes
            else if (roll < 30) type = 2; // carrots
            else type = 3; // potatos

            var plantedCrops = await Farm.InsertCropsInPlot(dbUser, type, plot_id);

            if (plantedCrops is null)
            {
                embed.Description = $"Sorry, there was an error in this operation. Not planted.";
                await interaction.RespondAsync(embed: embed.Build());
                return;
            }

            embed.Description = $"Seeds planted in plot {plot_id}";

            embed.AddField("What now?", "Seeds are a mystery! You won't know what you just planted until it grows. Remember to check into your crops to water them, this will make them finish growing sooner.");

            embed.AddField("Growth time", $"Without watering them, your crops will finish growing <t:{plantedCrops.unixGrowth}:R>", true);
            embed.AddField("Water time", $"You will be able to water these crops <t:{plantedCrops.unixNextWater}:R>", true);

            ModifyItem(dbUser, "seedbag", -1);
            embed.Footer = new EmbedFooterBuilder() { Text = $"1 {GetItemName("seedbag")} used." };

            int damage = 5 + rand.Next(8);

            toolStatus -= damage;

            ModifyItem(dbUser, "farmtools", -damage);

            if (toolStatus <= 0)
            {
                embed.AddField($"{GetItemName("farmtools")} destroyed.", $"Your {GetItemName("farmtools")} broke during this activity, you must get new ones.");
            }

            if (originalMsg is not null)
            {
                await originalMsg.ModifyAsync(x => x.Embed = embed.Build());
                await originalMsg.ModifyAsync(x => x.Components = comps.Build());
                await interaction.DeferAsync();
            }
            else
            {
                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
            }
        }

        public static async Task WaterPlots(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Watering crops";

            int count = 0;
            Random rand = new();

            SocketUserMessage? originalMsg = GetOriginalMessage(interaction, dbUser);

            List<Crop> cropsToList = (await _interface.GetUserCrops(dbUser)).ToList();
            foreach (var crop in cropsToList)
            {
                if (crop.unixNextWater < Global.CurrentUnix())
                {
                    crop.unixNextWater = Global.CurrentUnix() + 1800 + (1800 * rand.Next(4));
                    crop.unixGrowth -= 3600 + (300 * rand.Next(4));
                    await _interface.UpdateCrop(crop);
                    if (crop.unixGrowth < Global.CurrentUnix())
                    {
                        embed.AddField($"Crops in plot {crop.plotId} watered!", $"The crops in this plot have finished growing!");
                    }
                    else
                    {
                        embed.AddField($"Crops in plot {crop.plotId} watered!", $"They will now finish growing <t:{crop.unixGrowth}:R>. You may water it again <t:{crop.unixNextWater}:R>");
                    }
                    count++;
                }
            }
            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            int expIncrease = 5 * count;

            if (count > 0)
            {
                int foundPet = await RollForPet(dbUser);

                if (foundPet > 0)
                {
                    embed.AddField("You found a pet!", $"While watering your crops, you found a friendly {PetNames(foundPet)}, who chased you about. It has been added to your pets.");
                    buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);
                    expIncrease *= 5;
                    expIncrease /= 2;
                }
            }

            embed.Footer = new EmbedFooterBuilder() { Text = $"{dbUser.AddExp(expIncrease)} | {count} plot{((count != 1) ? 's' : null)} watered." };

            AddStandardButtons(ref buttonRow);

            comps.AddRow(buttonRow);

            if (originalMsg is not null)
            {
                await originalMsg.ModifyAsync(x => x.Embed = embed.Build());
                await originalMsg.ModifyAsync(x => x.Components = comps.Build());
                await interaction.DeferAsync();
            }
            else
            {
                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
            }
        }

        public static async Task HarvestPlots(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Harvesting crops";

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            buttonRow.WithButton(label: "Return to farm", customId: "farm", style: ButtonStyle.Secondary);
            buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            var toolStatus = await GetItem(dbUser, "farmtools");

            if (toolStatus <= 0)
            {
                embed.Title = $"{GetItemName("farmtools")} broken!";
                embed.Description = $"Your {GetItemName("farmtools")} are broken, you need new ones.";

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
            }

            int count = 0;
            Random rand = new();

            SocketUserMessage? originalMsg = GetOriginalMessage(interaction, dbUser);

            int expIncrease = 0;

            List<Crop> cropsToList = (await _interface.GetUserCrops(dbUser)).ToList();
            foreach (var crop in cropsToList)
            {
                if (crop.unixGrowth < Global.CurrentUnix())
                {
                    var success = await _interface.DeleteCrop(crop);
                    if (success is false)
                    {
                        // quiet fail, but it will be reported above
                        continue;
                    }
                    string harvest = Farm.GetHarvest(crop.cropType);
                    int earnings = 3 + rand.Next(4) * 3 + rand.Next(4) * 3;
                    ModifyItem(dbUser, harvest, +earnings);
                    expIncrease += earnings;
                    embed.AddField($"Plot {crop.plotId} harvested!", $"You have obtained {earnings} {GetItemName(harvest)}.");
                    count++;
                }
            }

            if (count > 0)
            {
                int foundPet = await RollForPet(dbUser);

                if (foundPet > 0)
                {
                    embed.AddField("You found a pet!", $"While harvesting your crops, you found a friendly {PetNames(foundPet)}, who chased you about. It has been added to your pets.");
                    buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);
                    expIncrease *= 5;
                    expIncrease /= 2;
                }
            }

            embed.Footer = new EmbedFooterBuilder() { Text = $"{dbUser.AddExp(expIncrease)} | {count} plot{((count != 1) ? 's' : null)} harvested." };

            AddStandardButtons(ref buttonRow);

            comps.AddRow(buttonRow);

            int damage = 5 + rand.Next(8);

            toolStatus -= damage;

            ModifyItem(dbUser, "farmtools", -damage);

            if (toolStatus <= 0)
            {
                embed.AddField($"{GetItemName("farmtools")} destroyed.", $"Your {GetItemName("farmtools")} broke during this activity, you must get new ones.");
            }

            if (originalMsg is not null)
            {
                await originalMsg.ModifyAsync(x => x.Embed = embed.Build());
                await originalMsg.ModifyAsync(x => x.Components = comps.Build());
                await interaction.DeferAsync();
            }
            else
            {
                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
            }
        }

        public static async Task ShowInventoryFunc(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Inventory";
            embed.Description = "Loading inventory...";

            await interaction.RespondAsync(embed: embed.Build());

            List<string> fieldsToList = new();

            EmbedFooterBuilder footer = new() { Text = $"{await RpgEngine.GetItem(dbUser, "dabloons")} {RpgEngine.GetItemName("dabloons")} | {await RpgEngine.GetItem(dbUser, "seedbag")} {RpgEngine.GetItemName("seedbag")}\n{dbUser.Exp} experience" };

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

            AddStandardButtons(ref buttonRow, except: "inventory");
            buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
            await interaction.ModifyOriginalResponseAsync(x => x.Components = comps.Build());
        }

        public static async Task ShowPets(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Pets";

            SocketUserMessage? originalMsg = GetOriginalMessage(interaction, dbUser);

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

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            SelectMenuBuilder petMenu = new()
            {
                Placeholder = "Set default pet",
                CustomId = "defaultPet"
            };
            petMenu.AddOption(label: "None", value: "0");
            foreach (int pet in petList)
            {
                petMenu.AddOption(label: PetNames(pet), value: $"{pet}");
            }

            petMenu.MaxValues = 1;

            comps.WithSelectMenu(petMenu);
            AddStandardButtons(ref buttonRow);

            comps.AddRow(buttonRow);

            if (originalMsg is not null)
            {
                await originalMsg.ModifyAsync(x => x.Embed = embed.Build());
                await originalMsg.ModifyAsync(x => x.Components = comps.Build());
                await interaction.DeferAsync();
            }
            else
            {
                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
            }
        }

        public static async Task ShowShopFunc(SocketInteraction interaction, SocketUser user)
        {
            var dbUser = await UserEngine.GetDBUser(user);
            if (dbUser is null) return;

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
            embed.Title = "Rosettes shop!";
            embed.Description = $"The shop allows for buying and selling items for dabloons.";

            embed.Footer = new EmbedFooterBuilder() { Text = $"[{user.Username}] has: {await RpgEngine.GetItem(dbUser, "dabloons")} {RpgEngine.GetItemName("dabloons")}" };

            var comps = GetShopComponents();

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }

        private static ComponentBuilder GetShopComponents()
        {
            SelectMenuBuilder buyMenu = new()
            {
                Placeholder = "Buy...",
                CustomId = "buy",
                MinValues = 1,
                MaxValues = 1
            };
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("seedbag")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "buy1");
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("fishpole")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "buy2");
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("farmtools")}", description: $"10 {RpgEngine.GetItemName("dabloons")}", value: "buy3");

            buyMenu.AddOption(label: $"1 🌿 plot of land", description: $"200 {RpgEngine.GetItemName("dabloons")}", value: "buy4");
            buyMenu.MaxValues = 1;

            SelectMenuBuilder sellMenu = new()
            {
                Placeholder = "Sell...",
                CustomId = "sell",
                MinValues = 1,
                MaxValues = 1
            };
            sellMenu.AddOption(label: $"5 {RpgEngine.GetItemName("fish")}", description: $"3 {RpgEngine.GetItemName("dabloons")}", value: "sell1");
            sellMenu.AddOption(label: $"5 {RpgEngine.GetItemName("uncommonfish")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell2");
            sellMenu.AddOption(label: $"1 {RpgEngine.GetItemName("rarefish")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell3");
            sellMenu.AddOption(label: $"10 {RpgEngine.GetItemName("tomato")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell4");
            sellMenu.AddOption(label: $"10 {RpgEngine.GetItemName("carrot")}", description: $"4 {RpgEngine.GetItemName("dabloons")}", value: "sell5");
            sellMenu.AddOption(label: $"10 {RpgEngine.GetItemName("potato")}", description: $"3 {RpgEngine.GetItemName("dabloons")}", value: "sell6");
            sellMenu.AddOption(label: $"10 {RpgEngine.GetItemName("garbage")}", description: $"2 {RpgEngine.GetItemName("dabloons")}", value: "sell7");
            sellMenu.MaxValues = 1;

            ActionRowBuilder buttonRow = new();

            AddStandardButtons(ref buttonRow, except: "shop");

            return new ComponentBuilder().WithSelectMenu(buyMenu, 0)
                .WithSelectMenu(sellMenu, 0)
                .AddRow(buttonRow);
        }

        private static SocketUserMessage? GetOriginalMessage(SocketInteraction interaction, User dbUser)
        {
            if (interaction is SocketMessageComponent)
            {
                SocketMessageComponent? inter = interaction as SocketMessageComponent;
                if (inter is not null)
                {
                    if (inter.Message.Author.Id != dbUser.Id) return null;
                    return inter.Message;
                }
            }
            return null;
        }

        private static void AddStandardButtons(ref ActionRowBuilder buttonRow, string except = "none")
        {
            if (except != "fish") buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary);
            if (except != "farm") buttonRow.WithButton(label: "Farm", customId: "farm", style: ButtonStyle.Primary);
            if (except != "shop") buttonRow.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Secondary);
            if (except != "inventory") buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);
        }
    }
}