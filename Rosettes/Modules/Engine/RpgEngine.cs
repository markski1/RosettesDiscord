﻿using Discord.Interactions;
using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Modules.Engine.RPG;
using static System.Reflection.Metadata.BlobBuilder;

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
            string[] choices = { "sushi" };
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
                                ModifyItem(dbUser, "dabloons", -5);
                                ModifyItem(dbUser, "fishpole", +1);
                                embed.Description = $"You have purchased {GetItemName("fishpole")} for 5 {GetItemName("dabloons")}";
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
                                ModifyItem(dbUser, "dabloons", -10);
                                ModifyItem(dbUser, "farmtools", +1);
                                embed.Description = $"You have purchased {GetItemName("uncommonfish")} for 10 {GetItemName("dabloons")}";
                            }
                            else
                            {
                                embed.Description = $"You don't have 10 {GetItemName("dabloons")}";
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
        }

        public static async Task SetDefaultPet(SocketMessageComponent component)
        {
            var dbUser = await UserEngine.GetDBUser(component.User);

            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            int petRequested = Int32.Parse(component.Data.Values.Last());

            if (petRequested < 1 || petRequested > 19)
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

            if (rand.Next(50) == 0)
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
            embed.Description = "Loading farm...";

            await interaction.RespondAsync(embed: embed.Build());

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
                        embed.AddField($"Plot {i}", $"There is {GetItemName(Farm.GetHarvest(currentCrop.cropType))} growing in this plot.\n It can be watered again <t:{currentCrop.unixNextWater}:R>. It'll be ready to harvest <t:{currentCrop.unixGrowth}:R>");
                    }
                }
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
                buttonRow.WithButton(label: "Plant crops", customId: "crops_plant", style: ButtonStyle.Primary);
            }
            if (anyCanBeWatered)
            {
                buttonRow.WithButton(label: "Water crops", customId: "crops_water", style: ButtonStyle.Primary);
            }
            buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
            await interaction.ModifyOriginalResponseAsync(msg => msg.Components = comps.Build());
        }

        public static async Task PlantPlot(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Plant plot";

            List<Crop> fieldsToList = (await _interface.GetUserCrops(dbUser)).ToList();

            int plots = await _interface.FetchInventoryItem(dbUser, "plots");

            if (fieldsToList.Count >= plots)
            {
                embed.Description = "You don't have any free plots to plant.";
                await interaction.RespondAsync(embed: embed.Build());
                return;
            }

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

            Random rand = new();

            var plantedCrops = await Farm.InsertCropsInPlot(dbUser, rand.Next(3)+1 ,plot_id);

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

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            buttonRow.WithButton(label: "Farm", customId: "farm", style: ButtonStyle.Secondary);
            buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }

        public static async Task WaterPlots(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Watering crops";

            int count = 0;
            Random rand = new();

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
            embed.Footer = new EmbedFooterBuilder() { Text = $"{count} plot{((count != 1) ? 's' : null)} watered." };

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            buttonRow.WithButton(label: "Farm", customId: "farm", style: ButtonStyle.Secondary);
            buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }

        public static async Task HarvestPlots(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Harvesting crops";

            int count = 0;
            Random rand = new();

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
                    embed.AddField($"Plot {crop.plotId} harvested!", $"You have obtained {earnings} {GetItemName(harvest)}.");
                    count++;
                }
            }
            embed.Footer = new EmbedFooterBuilder() { Text = $"{count} plot{((count != 1) ? 's' : null)} harvested." };

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            buttonRow.WithButton(label: "Back to farm", customId: "farm", style: ButtonStyle.Secondary);
            buttonRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);

            comps.AddRow(buttonRow);

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
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

            await interaction.ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            buttonRow.WithButton(label: "Fish", customId: "fish", style: ButtonStyle.Primary);
            buttonRow.WithButton(label: "Farm", customId: "farm", style: ButtonStyle.Primary);
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
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("seedbag")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "buy1");
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("fishpole")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "buy2");
            buyMenu.AddOption(label: $"1 {RpgEngine.GetItemName("farmtools")}", description: $"10 {RpgEngine.GetItemName("dabloons")}", value: "buy3");
            buyMenu.MaxValues = 1;

            SelectMenuBuilder sellMenu = new()
            {
                Placeholder = "Sell...",
                CustomId = "sell"
            };
            sellMenu.AddOption(label: $"5 {RpgEngine.GetItemName("fish")}", description: $"3 {RpgEngine.GetItemName("dabloons")}", value: "sell1");
            sellMenu.AddOption(label: $"5 {RpgEngine.GetItemName("uncommonfish")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell2");
            sellMenu.AddOption(label: $"1 {RpgEngine.GetItemName("rarefish")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell3");
            sellMenu.AddOption(label: $"10 {RpgEngine.GetItemName("tomato")}", description: $"5 {RpgEngine.GetItemName("dabloons")}", value: "sell4");
            sellMenu.AddOption(label: $"10 {RpgEngine.GetItemName("carrot")}", description: $"4 {RpgEngine.GetItemName("dabloons")}", value: "sell5");
            sellMenu.AddOption(label: $"10 {RpgEngine.GetItemName("potato")}", description: $"3 {RpgEngine.GetItemName("dabloons")}", value: "sell6");
            sellMenu.AddOption(label: $"10 {RpgEngine.GetItemName("garbage")}", description: $"2 {RpgEngine.GetItemName("dabloons")}", value: "sell7");
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