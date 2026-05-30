using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Minigame.Pets;

// ReSharper disable InconsistentNaming

namespace Rosettes.Modules.Minigame.Farming;

public static class Farm
{
    private const int SecondsBuffer = 3;

    private const int WaterBaseSeconds = 1800;              // 30 minutes
    private const int WaterRandomChunkSeconds = 300;        // 5 minutes
    private const int WaterRandomChunks = 4;

    private const int GrowthHoursBase = 3;
    private const int GrowthHoursRandomMaxExclusive = 4;
    private const int GrowthWateringReductionSeconds = 3600;

    private static string Pluralize(int count, string singular, string plural) =>
        count == 1 ? singular : plural;

    private static int Now() => Global.CurrentUnix();

    // Quality tiers (affect yield)
    private enum CropQuality
    {
        Bad,
        Good,
        Great
    }

    private static Task<CropQuality> RollCropQuality(User dbUser)
    {
        // Target odds:
        // Great: 15%
        // Bad:   20%
        // Good:  65%
        int roll = Global.Randomize(100); // 0..99

        if (roll < 15) return Task.FromResult(CropQuality.Great);
        if (roll < 35) return Task.FromResult(CropQuality.Bad);

        return Task.FromResult(CropQuality.Good);
    }

    private static int ApplyQualityMultiplier(int baseAmount, CropQuality quality)
    {
        // Good = 1x, Bad = 0.75x, Great = 1.25x
        return quality switch
        {
            CropQuality.Bad => (baseAmount * 3) / 4,
            CropQuality.Great => (baseAmount * 5) / 4,
            _ => baseAmount
        };
    }

    private static string QualityLabel(CropQuality quality) =>
        quality switch
        {
            CropQuality.Great => "Great",
            CropQuality.Bad => "Bad",
            _ => "Good"
        };

    private static string QualityStars(CropQuality quality) =>
        quality switch
        {
            CropQuality.Great => "⭐⭐⭐",
            CropQuality.Bad => "⭐",
            _ => "⭐⭐"
        };

    private static async Task<Crop?> InsertCropsInPlot(User dbUser, int cropType, int plotId)
    {
        // 3-second buffers on each to print a rounded-up time.
        var now = Now();
        int growTime = now + (3600 * GrowthHoursBase) + (3600 * Global.Randomize(GrowthHoursRandomMaxExclusive)) + SecondsBuffer;
        int waterTime = now + WaterBaseSeconds + SecondsBuffer;

        Crop newCrop = new(plotId, dbUser.Id, growTime, waterTime, cropType);

        bool success = await FarmRepository.InsertCrop(newCrop);
        return success ? newCrop : null;
    }

    private static string GetHarvest(int id) =>
        id switch
        {
            1 => "tomato",
            2 => "carrot",
            3 => "potato",
            _ => "invalid crop"
        };

    public static async Task ShowFarm(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);
        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser, FarmEngine.FarmColor);

        Global.AddTitle(container, "### 🌾 Your Farm");

        List<Crop> crops = (await FarmRepository.GetUserCrops(dbUser)).ToList();
        var cropsByPlot = crops.ToDictionary(c => c.PlotId);

        int plots = await FarmRepository.FetchInventoryItem(dbUser, "plots");

        bool anyCanBePlanted = false;
        bool anyCanBeWatered = false;
        bool anyCanBeHarvested = false;

        int now = Now();

        string plotLines = "";

        for (int i = 1; i <= plots; i++)
        {
            if (!cropsByPlot.TryGetValue(i, out var currentCrop))
            {
                plotLines += $"🟫 **Plot {i}** - *Empty, ready for seeds.*\n";
                anyCanBePlanted = true;
            }
            else if (currentCrop.UnixGrowth < now)
            {
                plotLines += $"{CropEmoji(currentCrop.CropType)} **Plot {i}** - **Ready to harvest.**\n";
                anyCanBeHarvested = true;
            }
            else if (currentCrop.UnixNextWater < now)
            {
                plotLines += $"💧 **Plot {i}** - *Needs water.* - Harvest <t:{currentCrop.UnixGrowth}:R>\n";
                anyCanBeWatered = true;
            }
            else
            {
                string extra = currentCrop.UnixGrowth > currentCrop.UnixNextWater
                    ? $" - Water <t:{currentCrop.UnixNextWater}:R>"
                    : "";
                plotLines += $"🌱 **Plot {i}** - Harvest <t:{currentCrop.UnixGrowth}:R>{extra}\n";
            }
        }

        container.WithTextDisplay(plotLines.TrimEnd());

        string fishText = dbUser.GetFishTime() < now
            ? "*You can fish right now.*"
            : $"Available again <t:{dbUser.GetFishTime()}:R>.";

        container.WithTextDisplay($"🎣 **Fishing Pond** - {fishText}");

        int seeds = await FarmRepository.FetchInventoryItem(dbUser, "seedbag");
        int dabloons = await FarmEngine.GetItem(dbUser, "dabloons");
        int occupied = cropsByPlot.Count;

        Global.AddFooter(container, $"🌱 {seeds} seeds  •  🐾 {dabloons} dabloons  •  🌿 {occupied}/{plots} plots");

        if (anyCanBeHarvested || anyCanBePlanted || anyCanBeWatered)
        {
            ActionRowBuilder farmActionRow = new();
            if (anyCanBeHarvested)
                farmActionRow.WithButton(label: "Harvest", customId: "crops_harvest", style: ButtonStyle.Success, emote: new Emoji("🌾"));
            if (anyCanBeWatered)
                farmActionRow.WithButton(label: "Water", customId: "crops_water", style: ButtonStyle.Success, emote: new Emoji("💧"));
            if (anyCanBePlanted)
                farmActionRow.WithButton(label: "Plant", customId: "crops_plant", style: ButtonStyle.Success, emote: new Emoji("🌱"));
            container.WithActionRow(farmActionRow);
        }

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, except: "farm");
        container.WithActionRow(buttonRow);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await interaction.RespondAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    private static string CropEmoji(int cropType) =>
        cropType switch
        {
            1 => "🍅",
            2 => "🥕",
            3 => "🥔",
            _ => "🌾"
        };

    public static async Task PlantSeed(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);

        var toolStatus = await FarmEngine.GetItem(dbUser, "farmtools");

        if (toolStatus <= 0)
        {
            ContainerBuilder errorContainer = await Global.MakeRosettesContainer(dbUser, FarmEngine.ErrorColor);
            errorContainer.WithTextDisplay($"🧰 {FarmEngine.GetItemName("farmtools")} broken");
            errorContainer.WithTextDisplay($"Your {FarmEngine.GetItemName("farmtools")} are broken. Visit the shop for a new set.");

            ActionRowBuilder errorButtons = new();
            FarmEngine.AddStandardButtons(ref errorButtons);
            errorContainer.WithActionRow(errorButtons);

            ComponentBuilderV2 errorComps = new();
            errorComps.WithContainer(errorContainer);

            await interaction.RespondAsync(components: errorComps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
            return;
        }

        List<Crop> fieldsToList = (await FarmRepository.GetUserCrops(dbUser)).ToList();

        int plots = await FarmRepository.FetchInventoryItem(dbUser, "plots");

        int seeds = await FarmRepository.FetchInventoryItem(dbUser, "seedbag");

        if (seeds <= 0)
        {
            ContainerBuilder errorContainer = await Global.MakeRosettesContainer(dbUser, FarmEngine.ErrorColor);
            errorContainer.WithTextDisplay("🌱 Out of seeds");
            errorContainer.WithTextDisplay("You have no seed bags. Pick some up at the shop.");

            ActionRowBuilder failRow = new();
            failRow.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Primary, emote: new Emoji("🛒"));
            failRow.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary, emote: new Emoji("🎒"));
            errorContainer.WithActionRow(failRow);

            ComponentBuilderV2 failComps = new();
            failComps.WithContainer(errorContainer);

            await interaction.RespondAsync(components: failComps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
            return;
        }

        List<int> occupiedPlots = [];
        occupiedPlots.AddRange(fieldsToList.Select(field => field.PlotId));

        if (occupiedPlots.Count >= plots)
        {
            ContainerBuilder errorContainer = await Global.MakeRosettesContainer(dbUser, FarmEngine.ErrorColor);
            errorContainer.WithTextDisplay("🌿 No space to plant");
            errorContainer.WithTextDisplay("All your plots are currently occupied.");

            ComponentBuilderV2 errorComps = new();
            errorComps.WithContainer(errorContainer);

            await interaction.RespondAsync(components: errorComps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
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
                ContainerBuilder errorContainer = await Global.MakeRosettesContainer(dbUser, FarmEngine.ErrorColor);
                errorContainer.WithTextDisplay("Sorry, there was an error in this operation. Not planted.");

                ComponentBuilderV2 errorComps = new();
                errorComps.WithContainer(errorContainer);

                await interaction.RespondAsync(components: errorComps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
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

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser, FarmEngine.FarmColor);
        Global.AddTitle(container, "### 🌱 Planting seeds");

        foreach (var plot in plantedCrops)
        {
            container.WithTextDisplay($"**🌱 Plot {plot.PlotId}**\nHarvest <t:{plot.UnixGrowth}:R>\nWater <t:{plot.UnixNextWater}:R>");
        }

        if (toolsBroken)
        {
            container.WithTextDisplay($"**🧰 {FarmEngine.GetItemName("farmtools")} destroyed**\nYour tools broke while planting. Pick up a new set at the shop.");
        }

        if (ranOutSeeds)
        {
            container.WithTextDisplay($"**🌱 Out of {FarmEngine.GetItemName("seedbag")}**\nSome plots were left unplanted. Restock at the shop.");
        }

        Global.AddFooter(container, $"{dbUser.AddExp(plantedCrops.Count * 5)}  •  🌱 {plantedCrops.Count} seed{(plantedCrops.Count == 1 ? "" : "s")} planted");

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, except: "farm");
        container.WithActionRow(buttonRow);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await interaction.RespondAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    public static async Task WaterCrops(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);

        int count = 0;
        bool cropsToHarvest = false;

        int now = Now();

        List<Crop> cropsToList = (await FarmRepository.GetUserCrops(dbUser)).ToList();

        List<string> plotTexts = [];

        foreach (var crop in cropsToList.Where(crop => crop.UnixNextWater < now))
        {
            crop.UnixNextWater = now + WaterBaseSeconds + (WaterRandomChunkSeconds * Global.Randomize(WaterRandomChunks));
            crop.UnixGrowth -= GrowthWateringReductionSeconds;

            await FarmRepository.UpdateCrop(crop);

            string text;
            if (crop.UnixGrowth < now)
            {
                text = $"**💧 Plot {crop.PlotId}**\n**Ready to harvest.**";
                cropsToHarvest = true;
            }
            else
            {
                text = $"**💧 Plot {crop.PlotId}**\nHarvest <t:{crop.UnixGrowth}:R>";
                if (crop.UnixGrowth > crop.UnixNextWater)
                    text += $"\nWater <t:{crop.UnixNextWater}:R>";
            }
            plotTexts.Add(text);

            count++;
        }

        if (count == 0)
        {
            ContainerBuilder errorContainer = await Global.MakeRosettesContainer(dbUser, FarmEngine.ErrorColor);
            errorContainer.WithTextDisplay("💧 Nothing to water");
            errorContainer.WithTextDisplay("None of your plots are thirsty right now.");

            ComponentBuilderV2 errorComps = new();
            errorComps.WithContainer(errorContainer);

            await interaction.RespondAsync(components: errorComps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
            return;
        }

        int expIncrease = 5 * count;

        int foundPet = await PetEngine.RollForPet(dbUser);
        if (foundPet > 0)
            expIncrease = (expIncrease * 5) / 2;

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser, FarmEngine.WaterColor);
        Global.AddTitle(container, "### 💧 Watering crops");

        foreach (var text in plotTexts)
            container.WithTextDisplay(text);

        if (foundPet > 0)
            container.WithTextDisplay($"**✨ You found a pet.**\nA friendly {PetEngine.PetNames(foundPet)} chased you about while you watered. It's been added to your pets.");

        Global.AddFooter(container, $"{dbUser.AddExp(expIncrease)}  •  💧 {count} {Pluralize(count, "plot", "plots")} watered");

        if (cropsToHarvest)
        {
            ActionRowBuilder harvestRow = new();
            harvestRow.WithButton(label: "Harvest", customId: "crops_harvest", style: ButtonStyle.Success, emote: new Emoji("🌾"));
            container.WithActionRow(harvestRow);
        }

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, except: "farm");
        if (foundPet > 0)
            buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary, emote: new Emoji("🐾"));
        container.WithActionRow(buttonRow);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await interaction.RespondAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    public static async Task HarvestCrops(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);

        var toolStatus = await FarmEngine.GetItem(dbUser, "farmtools");
        if (toolStatus <= 0)
        {
            ContainerBuilder errorContainer = await Global.MakeRosettesContainer(dbUser, FarmEngine.ErrorColor);
            errorContainer.WithTextDisplay($"🧰 {FarmEngine.GetItemName("farmtools")} broken");
            errorContainer.WithTextDisplay($"Your {FarmEngine.GetItemName("farmtools")} are broken. Visit the shop for a new set.");

            ActionRowBuilder brokenRow = new();
            FarmEngine.AddStandardButtons(ref brokenRow);
            errorContainer.WithActionRow(brokenRow);

            ComponentBuilderV2 errorComps = new();
            errorComps.WithContainer(errorContainer);

            await interaction.RespondAsync(components: errorComps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
            return;
        }

        int now = Now();

        int count = 0;
        int expIncrease = 0;
        bool plotsWereHarvested = false;

        List<string> harvestTexts = [];

        List<Crop> cropsToList = (await FarmRepository.GetUserCrops(dbUser)).ToList();
        foreach (var crop in cropsToList.Where(crop => crop.UnixGrowth < now))
        {
            var success = await FarmRepository.DeleteCrop(crop);
            if (!success)
            {
                continue;
            }

            string harvest = GetHarvest(crop.CropType);

            int baseEarnings = 9 + Global.Randomize(4) * 3 + Global.Randomize(4) * 3;

            var quality = await RollCropQuality(dbUser);
            int earnings = ApplyQualityMultiplier(baseEarnings, quality);

            FarmEngine.ModifyItem(dbUser, harvest, +earnings);
            expIncrease += earnings;

            string qualityLine = earnings == baseEarnings
                ? $"{QualityStars(quality)} *{QualityLabel(quality)}*"
                : $"{QualityStars(quality)} *{QualityLabel(quality)}* ({baseEarnings} → {earnings})";

            harvestTexts.Add($"**{CropEmoji(crop.CropType)} Plot {crop.PlotId}**\n**+{earnings}** {FarmEngine.GetItemName(harvest)}\n{qualityLine}");

            count++;
            plotsWereHarvested = true;
        }

        if (count == 0)
        {
            ContainerBuilder errorContainer = await Global.MakeRosettesContainer(dbUser, FarmEngine.ErrorColor);
            errorContainer.WithTextDisplay("🌾 Nothing to harvest");
            errorContainer.WithTextDisplay("None of your crops are ready yet.");

            ComponentBuilderV2 errorComps = new();
            errorComps.WithContainer(errorContainer);

            await interaction.RespondAsync(components: errorComps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
            return;
        }

        int foundPet = await PetEngine.RollForPet(dbUser);
        if (foundPet > 0)
            expIncrease = (expIncrease * 5) / 2;

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser, FarmEngine.HarvestColor);
        Global.AddTitle(container, "### 🌾 Harvesting crops");

        foreach (var text in harvestTexts)
            container.WithTextDisplay(text);

        if (foundPet > 0)
            container.WithTextDisplay($"**✨ You found a pet.**\nA friendly {PetEngine.PetNames(foundPet)} chased you about while you harvested. It's been added to your pets.");

        int damage = 3 + Global.Randomize(2);
        toolStatus -= damage;
        FarmEngine.ModifyItem(dbUser, "farmtools", -damage);

        if (toolStatus <= 0)
            container.WithTextDisplay($"**🧰 {FarmEngine.GetItemName("farmtools")} destroyed**\nYour tools broke while harvesting. Pick up a new set at the shop.");

        Global.AddFooter(container, $"{dbUser.AddExp(expIncrease)}  •  🌾 {count} {Pluralize(count, "plot", "plots")} harvested");

        if (plotsWereHarvested)
        {
            ActionRowBuilder plantRow = new();
            plantRow.WithButton(label: "Plant", customId: "crops_plant", style: ButtonStyle.Success, emote: new Emoji("🌱"));
            container.WithActionRow(plantRow);
        }

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, except: "farm");
        if (foundPet > 0)
            buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary, emote: new Emoji("🐾"));
        container.WithActionRow(buttonRow);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await interaction.RespondAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }
}

// Database constructor for crops.
public class Crop(int plot_id, ulong user_id, int unix_growth, int unix_next_water, int crop_type)
{
    public int PlotId { get; init; } = plot_id;
    public ulong UserId { get; init; } = user_id;
    public int UnixGrowth { get; set; } = unix_growth;
    public int UnixNextWater { get; set; } = unix_next_water;
    public int CropType { get; init; } = crop_type;
}
