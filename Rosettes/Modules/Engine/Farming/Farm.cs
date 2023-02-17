using Discord.WebSocket;
using Discord;
using Rosettes.Core;

namespace Rosettes.Modules.Engine.Farming
{
    public static class Farm
    {
        public static async Task<Crop?> InsertCropsInPlot(User dbUser, int cropType, int plot_id)
        {
            Random rand = new();
            // 5 second buffers on each to print a rounded-up time.
            int growTime = Global.CurrentUnix() + 3600 * 3 + 3600 * rand.Next(4) + 5;
            int waterTime = Global.CurrentUnix() + 1800 + 5;

            Crop newCrop = new(plot_id, dbUser.Id, growTime, waterTime, cropType);

            bool success = await FarmEngine._interface.InsertCrop(newCrop);

            if (success)
            {
                return newCrop;
            }
            else
            {
                return null;
            }
        }

        public static string GetHarvest(int id)
        {
            return id switch
            {
                1 => "tomato",
                2 => "carrot",
                3 => "potato",
                _ => "invalid crop"
            };
        }

        public static async Task PlantSeed(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Planting seeds";

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            var toolStatus = await FarmEngine.GetItem(dbUser, "farmtools");

            if (toolStatus <= 0)
            {
                embed.Title = $"{FarmEngine.GetItemName("farmtools")} broken.";
                embed.Description = $"Your {FarmEngine.GetItemName("farmtools")} are broken, you need new ones.";

                FarmEngine.AddStandardButtons(ref buttonRow);
                comps.AddRow(buttonRow);

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build(), ephemeral: true);
                return;
            }

            List<Crop> fieldsToList = (await FarmEngine._interface.GetUserCrops(dbUser)).ToList();

            int plots = await FarmEngine._interface.FetchInventoryItem(dbUser, "plots");

            int seeds = await FarmEngine._interface.FetchInventoryItem(dbUser, "seedbag");

            if (seeds <= 0)
            {
                embed.Description = "You don't have any seeds, you may obtain them at the shop.";
                ComponentBuilder failComps = new();

                ActionRowBuilder failButtons = new();

                failButtons.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Primary);
                failButtons.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary);

                failComps.AddRow(failButtons);
                await interaction.RespondAsync(embed: embed.Build(), components: failComps.Build(), ephemeral: true);
                return;
            }

            List<int> occupiedPlots = new();

            Random rand = new();

            foreach (var field in fieldsToList)
            {
                occupiedPlots.Add(field.plotId);
            }

            int plot_id = 1;
            while (occupiedPlots.Contains(plot_id)) plot_id++;

            if (plot_id > plots)
            {
                embed.Title = "No space to plant.";
                embed.Description = "All your plots of land are currently occupied.";

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build(), ephemeral: true);
                return;
            }

            int roll = rand.Next(55);
            int type;

            if (roll < 10) type = 1; // tomatoes
            else if (roll < 30) type = 2; // carrots
            else type = 3; // potatos

            var plantedCrops = await Farm.InsertCropsInPlot(dbUser, type, plot_id);

            if (plantedCrops is null)
            {
                embed.Description = $"Sorry, there was an error in this operation. Not planted.";
                await interaction.RespondAsync(embed: embed.Build(), ephemeral: true);
                return;
            }

            if (fieldsToList.Count + 1 < plots)
            {
                ActionRowBuilder actionRow = new();
                actionRow.WithButton(label: "Plant seeds", customId: "crops_plant", style: ButtonStyle.Success);
                comps.AddRow(actionRow);
            }

            FarmEngine.AddStandardButtons(ref buttonRow);
            comps.AddRow(buttonRow);

            embed.Description = $"Seeds planted in plot {plot_id}";

            embed.AddField("You'll know what your crops are when they grow.", "Remember to check into your crops to water them, this will make them grow faster.");

            embed.AddField("Growth time", $"Without watering them, your crops will finish growing <t:{plantedCrops.unixGrowth}:R>", true);
            embed.AddField("Water time", $"You will be able to water these crops <t:{plantedCrops.unixNextWater}:R>", true);

            FarmEngine.ModifyItem(dbUser, "seedbag", -1);
            embed.Footer = new EmbedFooterBuilder() { Text = $"{dbUser.AddExp(5)} | 1 {FarmEngine.GetItemName("seedbag")} used." };

            int damage = 3 + rand.Next(3);

            toolStatus -= damage;

            FarmEngine.ModifyItem(dbUser, "farmtools", -damage);

            if (toolStatus <= 0)
            {
                embed.AddField($"{FarmEngine.GetItemName("farmtools")} destroyed.", $"Your {FarmEngine.GetItemName("farmtools")} broke during this activity, you must get new ones.");
            }

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }

        public static async Task WaterCrops(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Watering crops";

            int count = 0;
            Random rand = new();

            bool cropsToHarvest = false;

            List<Crop> cropsToList = (await FarmEngine._interface.GetUserCrops(dbUser)).ToList();
            foreach (var crop in cropsToList)
            {
                if (crop.unixNextWater < Global.CurrentUnix())
                {
                    crop.unixNextWater = Global.CurrentUnix() + 1800 + 300 * rand.Next(4);
                    crop.unixGrowth -= 3600;
                    await FarmEngine._interface.UpdateCrop(crop);
                    if (crop.unixGrowth < Global.CurrentUnix())
                    {
                        embed.AddField($"🌿 Plot {crop.plotId} watered.", $"The crops in this plot have finished growing.");
                        cropsToHarvest = true;
                    }
                    else
                    {
                        string text = $"They will now finish growing <t:{crop.unixGrowth}:R>.";
                        if (crop.unixGrowth > crop.unixNextWater)
                        {
                            text += $" You may water them again <t:{crop.unixNextWater}:R>";
                        }
                        embed.AddField($"🌿 Plot {crop.plotId} watered.", text);
                    }
                    count++;
                }
            }
            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            ActionRowBuilder actionRow = new();

            if (cropsToHarvest)
            {
                actionRow.WithButton(label: "Harvest crops", customId: "crops_harvest", style: ButtonStyle.Success);
                comps.AddRow(actionRow);
            }

            int expIncrease = 5 * count;

            FarmEngine.AddStandardButtons(ref buttonRow);

            if (count > 0)
            {
                int foundPet = await FarmEngine.RollForPet(dbUser);

                if (foundPet > 0)
                {
                    embed.AddField("You found a pet!", $"While watering your crops, you found a friendly {FarmEngine.PetNames(foundPet)}, who chased you about. It has been added to your pets.");
                    buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);
                    expIncrease *= 5;
                    expIncrease /= 2;
                }
            }
            else
            {
                await interaction.RespondAsync("Nothing to water.", ephemeral: true);
                return;
            }

            embed.Footer = new EmbedFooterBuilder() { Text = $"{dbUser.AddExp(expIncrease)} | {count} plot{(count != 1 ? 's' : null)} watered." };

            comps.AddRow(buttonRow);

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }

        public static async Task HarvestCrops(SocketInteraction interaction, IUser user)
        {
            User dbUser = await UserEngine.GetDBUser(user);
            EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

            embed.Title = $"Harvesting crops";

            ComponentBuilder comps = new();

            ActionRowBuilder buttonRow = new();

            var toolStatus = await FarmEngine.GetItem(dbUser, "farmtools");

            if (toolStatus <= 0)
            {
                embed.Title = $"{FarmEngine.GetItemName("farmtools")} broken.";
                embed.Description = $"Your {FarmEngine.GetItemName("farmtools")} are broken, you need new ones.";

                FarmEngine.AddStandardButtons(ref buttonRow);

                comps.AddRow(buttonRow);

                await interaction.RespondAsync(embed: embed.Build(), components: comps.Build(), ephemeral: true);
                return;
            }

            int count = 0;
            Random rand = new();

            int expIncrease = 0;

            bool plotsWereHarvested = false;

            List<Crop> cropsToList = (await FarmEngine._interface.GetUserCrops(dbUser)).ToList();
            foreach (var crop in cropsToList)
            {
                if (crop.unixGrowth < Global.CurrentUnix())
                {
                    var success = await FarmEngine._interface.DeleteCrop(crop);
                    if (success is false)
                    {
                        // quiet fail, but it will be reported above
                        continue;
                    }
                    string harvest = Farm.GetHarvest(crop.cropType);
                    int earnings = 9 + rand.Next(4) * 3 + rand.Next(4) * 3;
                    FarmEngine.ModifyItem(dbUser, harvest, +earnings);
                    expIncrease += earnings;
                    embed.AddField($"🌿 Plot {crop.plotId} harvested.", $"You have obtained {earnings} {FarmEngine.GetItemName(harvest)}.");
                    count++;
                    plotsWereHarvested = true;
                }
            }

            if (count > 0)
            {
                int foundPet = await FarmEngine.RollForPet(dbUser);

                if (foundPet > 0)
                {
                    embed.AddField("You found a pet!", $"While harvesting your crops, you found a friendly {FarmEngine.PetNames(foundPet)}, who chased you about. It has been added to your pets.");
                    buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary);
                    expIncrease *= 5;
                    expIncrease /= 2;
                }
            }
            else
            {
                await interaction.RespondAsync("Nothing to harvest.", ephemeral: true);
                return;
            }

            ActionRowBuilder actionRow = new();

            if (plotsWereHarvested)
            {
                actionRow.WithButton(label: "Plant seeds", customId: "crops_plant", style: ButtonStyle.Success);
                comps.AddRow(actionRow);
            }

            comps.AddRow(buttonRow);

            FarmEngine.AddStandardButtons(ref buttonRow);

            embed.Footer = new EmbedFooterBuilder() { Text = $"{dbUser.AddExp(expIncrease)} | {count} plot{(count != 1 ? 's' : null)} harvested." };

            int damage = 3 + rand.Next(2);

            toolStatus -= damage;

            FarmEngine.ModifyItem(dbUser, "farmtools", -damage);

            if (toolStatus <= 0)
            {
                embed.AddField($"{FarmEngine.GetItemName("farmtools")} destroyed.", $"Your {FarmEngine.GetItemName("farmtools")} broke during this activity, you must get new ones.");
            }

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
        }
    }

    public class Crop
    {
        public int plotId;
        public ulong userId;
        public int unixGrowth;
        public int unixNextWater;
        public int cropType;

        public Crop(int plot_id, ulong user_id, int unix_growth, int unix_next_water, int crop_type)
        {
            plotId = plot_id;
            userId = user_id;
            unixGrowth = unix_growth;
            unixNextWater = unix_next_water;
            cropType = crop_type;
        }
    }
}
