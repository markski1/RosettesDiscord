using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;

namespace Rosettes.Modules.Engine.Minigame;

public static class Farm
{
    private static async Task<Crop?> InsertCropsInPlot(User dbUser, int cropType, int plotId)
    {
        // 3 second buffers on each to print a rounded-up time.
        int growTime = Global.CurrentUnix() + 3600 * 3 + 3600 * Global.Randomize(4) + 3;
        int waterTime = Global.CurrentUnix() + 1800 + 3;

        Crop newCrop = new(plotId, dbUser.Id, growTime, waterTime, cropType);

        bool success = await FarmRepository.InsertCrop(newCrop);

        if (success) return newCrop;
        return null;
    }

    private static string GetHarvest(int id)
    {
        return id switch
        {
            1 => "tomato",
            2 => "carrot",
            3 => "potato",
            _ => "invalid crop"
        };
    }

    public static async Task ShowFarm(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "Farm";

        List<Crop> fieldsToList = (await FarmRepository.GetUserCrops(dbUser)).ToList();

        int plots = await FarmRepository.FetchInventoryItem(dbUser, "plots");

        embed.Description = $"Your farm has {plots} plot{(plots != 1 ? 's' : null)} of land.";

        bool anyCanBePlanted = false;
        bool anyCanBeWatered = false;
        bool anyCanBeHarvested = false;

        int currentUnix = Global.CurrentUnix();

        for (int i = 1; i <= plots; i++)
        {
            Crop? currentCrop = fieldsToList.Find(x => x.PlotId == i);
            if (currentCrop is null)
            {
                embed.AddField($"🌿 Plot {i}", "There is nothing growing in this plot.", inline: i != 1); // Plot 1 is not inline, anything after is
                anyCanBePlanted = true;
            }
            else
            {
                bool canBeHarvested = false;
                bool canBeWatered = false;
                if (currentCrop.UnixGrowth < currentUnix)
                {
                    canBeHarvested = true;
                    anyCanBeHarvested = true;
                }
                else if (currentCrop.UnixNextWater < currentUnix)
                {
                    canBeWatered = true;
                    anyCanBeWatered = true;
                }

                string plotText;

                if (canBeWatered)
                {
                    plotText = $"Crops are growing in this plot.\n They can be watered right now.\nThey'll be ready to harvest <t:{currentCrop.UnixGrowth}:R>";
                }
                else if (canBeHarvested)
                {
                    plotText = $"{FarmEngine.GetItemName(GetHarvest(currentCrop.CropType))} has grown in this plot.\nThey can be harvested right now.";
                }
                else
                {
                    plotText = "Crops are growing in this plot.\n";
                    if (currentCrop.UnixGrowth > currentCrop.UnixNextWater)
                    {
                        plotText += $"They can be watered <t:{currentCrop.UnixNextWater}:R>\n";
                    }
                    plotText += $"They can be harvested <t:{currentCrop.UnixGrowth}:R>";
                }
                embed.AddField($"🌿 Plot {i}", plotText, inline: i != 1);
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

        ActionRowBuilder actionRow = new();

        if (anyCanBeHarvested || anyCanBePlanted || anyCanBeWatered)
        {
            if (anyCanBeHarvested)
            {
                actionRow.WithButton(label: "Harvest crops", customId: "crops_harvest", style: ButtonStyle.Success);
            }
            if (anyCanBePlanted)
            {
                actionRow.WithButton(label: "Plant seeds", customId: "crops_plant", style: ButtonStyle.Success);
            }
            if (anyCanBeWatered)
            {
                actionRow.WithButton(label: "Water crops", customId: "crops_water", style: ButtonStyle.Success);
            }
            comps.AddRow(actionRow);
        }

        FarmEngine.AddStandardButtons(ref buttonRow);

        comps.AddRow(buttonRow);

        await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
    }

    public static async Task PlantSeed(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);
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

        List<Crop> fieldsToList = (await FarmRepository.GetUserCrops(dbUser)).ToList();

        int plots = await FarmRepository.FetchInventoryItem(dbUser, "plots");

        int seeds = await FarmRepository.FetchInventoryItem(dbUser, "seedbag");

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

        List<int> occupiedPlots = [];
        occupiedPlots.AddRange(fieldsToList.Select(field => field.PlotId));

        if (occupiedPlots.Count >= plots)
        {
            embed.Title = "No space to plant.";
            embed.Description = "All your plots of land are currently occupied.";

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build(), ephemeral: true);
            return;
        }

        int freePlots = plots - occupiedPlots.Count;

        List<Crop> plantedCrops = [];

        bool ranOutSeeds = false;
        bool toolsBroken = false;

        for (int i = 0; i < freePlots; i++)
        {
            if (seeds == 0)
            {
                ranOutSeeds = true;
                break;
            }

            int plotId = 1;
            while (occupiedPlots.Contains(plotId)) plotId++;

            occupiedPlots.Add(plotId);

            if (plotId > plots) break;

            int roll = Global.Randomize(55);

            int type = roll switch
            {
                < 10 => 1,
                < 30 => 2,
                _ => 3
            };

            var plantedCrop = await InsertCropsInPlot(dbUser, type, plotId);

            if (plantedCrop is null)
            {
                embed.Description = "Sorry, there was an error in this operation. Not planted.";
                await interaction.RespondAsync(embed: embed.Build(), ephemeral: true);
                return;
            }

            plantedCrops.Add(plantedCrop);

            FarmEngine.ModifyItem(dbUser, "seedbag", -1);
            seeds--;

            int damage = 3 + Global.Randomize(3);
            toolStatus -= damage;
            FarmEngine.ModifyItem(dbUser, "farmtools", -damage);

            if (toolStatus > 0) continue;
            
            toolsBroken = true;
            break;
        }

        FarmEngine.AddStandardButtons(ref buttonRow);
        comps.AddRow(buttonRow);

        foreach (var plot in plantedCrops)
        {
            embed.AddField($"🌿 Plot {plot.PlotId}", $"Finishes growing <t:{plot.UnixGrowth}:R>\nCan be watered <t:{plot.UnixNextWater}:R>");
        }

        embed.Footer = new EmbedFooterBuilder { Text = $"{dbUser.AddExp(plantedCrops.Count * 5)} | {plantedCrops.Count} {FarmEngine.GetItemName("seedbag")} used." };

        if (toolsBroken)
        {
            embed.AddField($"{FarmEngine.GetItemName("farmtools")} destroyed.", $"Your {FarmEngine.GetItemName("farmtools")} broke during this activity, you must get new ones.");
        }

        if (ranOutSeeds)
        {
            embed.AddField($"Ran out of {FarmEngine.GetItemName("seedbag")}.", "One or more plots were left unplanted because you ran out of seed bags.");
        }

        await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
    }

    public static async Task WaterCrops(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = $"Watering crops";

        int count = 0;

        bool cropsToHarvest = false;

        List<Crop> cropsToList = (await FarmRepository.GetUserCrops(dbUser)).ToList();
        foreach (var crop in cropsToList)
        {
            if (crop.UnixNextWater >= Global.CurrentUnix()) continue;
            
            crop.UnixNextWater = Global.CurrentUnix() + 1800 + 300 * Global.Randomize(4);
            crop.UnixGrowth -= 3600;
            await FarmRepository.UpdateCrop(crop);
            if (crop.UnixGrowth < Global.CurrentUnix())
            {
                embed.AddField($"🌿 Plot {crop.PlotId} watered.", "The crops in this plot have finished growing.");
                cropsToHarvest = true;
            }
            else
            {
                string text = $"They will now finish growing <t:{crop.UnixGrowth}:R>.";
                if (crop.UnixGrowth > crop.UnixNextWater)
                {
                    text += $" You may water them again <t:{crop.UnixNextWater}:R>";
                }
                embed.AddField($"🌿 Plot {crop.PlotId} watered.", text);
            }
            count++;
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
            int foundPet = await PetEngine.RollForPet(dbUser);

            if (foundPet > 0)
            {
                embed.AddField("You found a pet!", $"While watering your crops, you found a friendly {PetEngine.PetNames(foundPet)}, who chased you about. It has been added to your pets.");
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

        embed.Footer = new EmbedFooterBuilder { Text = $"{dbUser.AddExp(expIncrease)} | {count} plot{(count != 1 ? 's' : null)} watered." };

        comps.AddRow(buttonRow);

        await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
    }

    public static async Task HarvestCrops(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "Harvesting crops";

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

        int expIncrease = 0;

        bool plotsWereHarvested = false;

        List<Crop> cropsToList = (await FarmRepository.GetUserCrops(dbUser)).ToList();
        foreach (var crop in cropsToList)
        {
            if (crop.UnixGrowth >= Global.CurrentUnix()) continue;
            
            var success = await FarmRepository.DeleteCrop(crop);
            if (success is false)
            {
                // quiet fail, but it will be reported above
                continue;
            }
            string harvest = GetHarvest(crop.CropType);
            int earnings = 9 + Global.Randomize(4) * 3 + Global.Randomize(4) * 3;
            FarmEngine.ModifyItem(dbUser, harvest, +earnings);
            expIncrease += earnings;
            embed.AddField($"🌿 Plot {crop.PlotId} harvested.", $"You have obtained {earnings} {FarmEngine.GetItemName(harvest)}.");
            count++;
            plotsWereHarvested = true;
        }

        if (count > 0)
        {
            int foundPet = await PetEngine.RollForPet(dbUser);

            if (foundPet > 0)
            {
                embed.AddField("You found a pet!", $"While harvesting your crops, you found a friendly {PetEngine.PetNames(foundPet)}, who chased you about. It has been added to your pets.");
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

        embed.Footer = new EmbedFooterBuilder { Text = $"{dbUser.AddExp(expIncrease)} | {count} plot{(count != 1 ? 's' : null)} harvested." };

        int damage = 3 + Global.Randomize(2);

        toolStatus -= damage;

        FarmEngine.ModifyItem(dbUser, "farmtools", -damage);

        if (toolStatus <= 0)
        {
            embed.AddField($"{FarmEngine.GetItemName("farmtools")} destroyed.", $"Your {FarmEngine.GetItemName("farmtools")} broke during this activity, you must get new ones.");
        }

        await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
    }
}

public class Crop(int plot_id, ulong user_id, int unix_growth, int unix_next_water, int crop_type)
{
    public int PlotId = plot_id;
    public ulong UserId = user_id;
    public int UnixGrowth = unix_growth;
    public int UnixNextWater = unix_next_water;
    public int CropType = crop_type;
}