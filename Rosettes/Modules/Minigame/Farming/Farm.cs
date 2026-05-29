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
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "🌾 Your Farm";
        embed.Color = FarmEngine.FarmColor;

        List<Crop> crops = (await FarmRepository.GetUserCrops(dbUser)).ToList();
        var cropsByPlot = crops.ToDictionary(c => c.PlotId);

        int plots = await FarmRepository.FetchInventoryItem(dbUser, "plots");

        bool anyCanBePlanted = false;
        bool anyCanBeWatered = false;
        bool anyCanBeHarvested = false;

        int now = Now();

        // Render plots as a 3-column grid (max plots = 3) for a clean layout.
        for (int i = 1; i <= plots; i++)
        {
            string title;
            string plotText;

            if (!cropsByPlot.TryGetValue(i, out var currentCrop))
            {
                title = $"🟫 Plot {i}";
                plotText = "*Empty, ready for seeds.*";
                anyCanBePlanted = true;
            }
            else if (currentCrop.UnixGrowth < now)
            {
                title = $"{CropEmoji(currentCrop.CropType)} Plot {i}";
                plotText = $"**Ready to harvest.**\n{FarmEngine.GetItemName(GetHarvest(currentCrop.CropType))}";
                anyCanBeHarvested = true;
            }
            else if (currentCrop.UnixNextWater < now)
            {
                title = $"💧 Plot {i}";
                plotText = $"*Needs water.*\nHarvest <t:{currentCrop.UnixGrowth}:R>";
                anyCanBeWatered = true;
            }
            else
            {
                title = $"🌱 Plot {i}";
                plotText = $"Harvest <t:{currentCrop.UnixGrowth}:R>";
                if (currentCrop.UnixGrowth > currentCrop.UnixNextWater)
                    plotText += $"\nWater <t:{currentCrop.UnixNextWater}:R>";
            }

            embed.AddField(title, plotText, inline: true);
        }

        // Fishing pond in its own row beneath the plots.
        embed.AddField(
            "🎣 Fishing Pond",
            dbUser.GetFishTime() < now
                ? "*You can fish right now.*"
                : $"Available again <t:{dbUser.GetFishTime()}:R>."
        );

        int seeds = await FarmRepository.FetchInventoryItem(dbUser, "seedbag");
        int dabloons = await FarmEngine.GetItem(dbUser, "dabloons");
        int occupied = cropsByPlot.Count;
        embed.Footer = new()
        {
            Text = $"🌱 {seeds} seeds  •  🐾 {dabloons} dabloons  •  🌿 {occupied}/{plots} plots in use"
        };

        ComponentBuilder comps = new();

        if (anyCanBeHarvested || anyCanBePlanted || anyCanBeWatered)
        {
            ActionRowBuilder actionRow = new();
            if (anyCanBeHarvested)
                actionRow.WithButton(label: "Harvest", customId: "crops_harvest", style: ButtonStyle.Success, emote: new Emoji("🌾"));
            if (anyCanBeWatered)
                actionRow.WithButton(label: "Water", customId: "crops_water", style: ButtonStyle.Success, emote: new Emoji("💧"));
            if (anyCanBePlanted)
                actionRow.WithButton(label: "Plant", customId: "crops_plant", style: ButtonStyle.Success, emote: new Emoji("🌱"));

            comps.AddRow(actionRow);
        }

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, except: "farm");
        comps.AddRow(buttonRow);

        await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
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
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "🌱 Planting seeds";
        embed.Color = FarmEngine.FarmColor;

        ComponentBuilder comps = new();

        ActionRowBuilder buttonRow = new();

        var toolStatus = await FarmEngine.GetItem(dbUser, "farmtools");

        if (toolStatus <= 0)
        {
            embed.Title = $"🧰 {FarmEngine.GetItemName("farmtools")} broken";
            embed.Description = $"Your {FarmEngine.GetItemName("farmtools")} are broken. Visit the shop for a new set.";
            embed.Color = FarmEngine.ErrorColor;

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
            embed.Title = "🌱 Out of seeds";
            embed.Description = "You have no seed bags. Pick some up at the shop.";
            embed.Color = FarmEngine.ErrorColor;

            ComponentBuilder failComps = new();
            ActionRowBuilder failButtons = new();

            failButtons.WithButton(label: "Shop", customId: "shop", style: ButtonStyle.Primary, emote: new Emoji("🛒"));
            failButtons.WithButton(label: "Inventory", customId: "inventory", style: ButtonStyle.Secondary, emote: new Emoji("🎒"));

            failComps.AddRow(failButtons);
            await interaction.RespondAsync(embed: embed.Build(), components: failComps.Build(), ephemeral: true);
            return;
        }

        List<int> occupiedPlots = [];
        occupiedPlots.AddRange(fieldsToList.Select(field => field.PlotId));

        if (occupiedPlots.Count >= plots)
        {
            embed.Title = "🌿 No space to plant";
            embed.Description = "All your plots are currently occupied.";
            embed.Color = FarmEngine.ErrorColor;

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

        FarmEngine.AddStandardButtons(ref buttonRow, except: "farm");
        comps.AddRow(buttonRow);

        foreach (var plot in plantedCrops)
        {
            embed.AddField(
                $"🌱 Plot {plot.PlotId}",
                $"Harvest <t:{plot.UnixGrowth}:R>\nWater <t:{plot.UnixNextWater}:R>",
                inline: true
            );
        }

        embed.Footer = new EmbedFooterBuilder
        {
            Text = $"{dbUser.AddExp(plantedCrops.Count * 5)}  •  🌱 {plantedCrops.Count} seed{(plantedCrops.Count == 1 ? "" : "s")} planted"
        };

        if (toolsBroken)
        {
            embed.AddField(
                $"🧰 {FarmEngine.GetItemName("farmtools")} destroyed",
                "Your tools broke while planting. Pick up a new set at the shop."
            );
        }

        if (ranOutSeeds)
        {
            embed.AddField(
                $"🌱 Out of {FarmEngine.GetItemName("seedbag")}",
                "Some plots were left unplanted. Restock at the shop."
            );
        }

        await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
    }

    public static async Task WaterCrops(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "💧 Watering crops";
        embed.Color = FarmEngine.WaterColor;

        int count = 0;
        bool cropsToHarvest = false;

        int now = Now();

        List<Crop> cropsToList = (await FarmRepository.GetUserCrops(dbUser)).ToList();

        foreach (var crop in cropsToList.Where(crop => crop.UnixNextWater < now))
        {
            crop.UnixNextWater = now + WaterBaseSeconds + (WaterRandomChunkSeconds * Global.Randomize(WaterRandomChunks));
            crop.UnixGrowth -= GrowthWateringReductionSeconds;

            await FarmRepository.UpdateCrop(crop);

            string title = $"💧 Plot {crop.PlotId}";
            string text;
            if (crop.UnixGrowth < now)
            {
                text = "**Ready to harvest.**";
                cropsToHarvest = true;
            }
            else
            {
                text = $"Harvest <t:{crop.UnixGrowth}:R>";
                if (crop.UnixGrowth > crop.UnixNextWater)
                    text += $"\nWater <t:{crop.UnixNextWater}:R>";
            }
            embed.AddField(title, text, inline: true);

            count++;
        }

        if (count == 0)
        {
            EmbedBuilder noneEmbed = await Global.MakeRosettesEmbed(dbUser);
            noneEmbed.Title = "💧 Nothing to water";
            noneEmbed.Description = "None of your plots are thirsty right now.";
            noneEmbed.Color = FarmEngine.ErrorColor;
            await interaction.RespondAsync(embed: noneEmbed.Build(), ephemeral: true);
            return;
        }

        ComponentBuilder comps = new();

        if (cropsToHarvest)
        {
            ActionRowBuilder actionRow = new();
            actionRow.WithButton(label: "Harvest", customId: "crops_harvest", style: ButtonStyle.Success, emote: new Emoji("🌾"));
            comps.AddRow(actionRow);
        }

        int expIncrease = 5 * count;

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, except: "farm");

        int foundPet = await PetEngine.RollForPet(dbUser);
        if (foundPet > 0)
        {
            embed.AddField(
                "✨ You found a pet.",
                $"A friendly {PetEngine.PetNames(foundPet)} chased you about while you watered. It's been added to your pets."
            );
            buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary, emote: new Emoji("🐾"));
            expIncrease = (expIncrease * 5) / 2;
        }

        embed.Footer = new EmbedFooterBuilder
        {
            Text = $"{dbUser.AddExp(expIncrease)}  •  💧 {count} {Pluralize(count, "plot", "plots")} watered"
        };

        comps.AddRow(buttonRow);
        await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
    }

    public static async Task HarvestCrops(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        embed.Title = "🌾 Harvesting crops";
        embed.Color = FarmEngine.HarvestColor;

        ComponentBuilder comps = new();

        var toolStatus = await FarmEngine.GetItem(dbUser, "farmtools");
        if (toolStatus <= 0)
        {
            embed.Title = $"🧰 {FarmEngine.GetItemName("farmtools")} broken";
            embed.Description = $"Your {FarmEngine.GetItemName("farmtools")} are broken. Visit the shop for a new set.";
            embed.Color = FarmEngine.ErrorColor;

            ActionRowBuilder brokenRow = new();
            FarmEngine.AddStandardButtons(ref brokenRow);
            comps.AddRow(brokenRow);

            await interaction.RespondAsync(embed: embed.Build(), components: comps.Build(), ephemeral: true);
            return;
        }

        int now = Now();

        int count = 0;
        int expIncrease = 0;
        bool plotsWereHarvested = false;

        List<Crop> cropsToList = (await FarmRepository.GetUserCrops(dbUser)).ToList();
        foreach (var crop in cropsToList.Where(crop => crop.UnixGrowth < now))
        {
            var success = await FarmRepository.DeleteCrop(crop);
            if (!success)
            {
                // quiet fail, but it will be reported above
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

            embed.AddField(
                $"{CropEmoji(crop.CropType)} Plot {crop.PlotId}",
                $"**+{earnings}** {FarmEngine.GetItemName(harvest)}\n{qualityLine}",
                inline: true
            );

            count++;
            plotsWereHarvested = true;
        }

        if (count == 0)
        {
            EmbedBuilder noneEmbed = await Global.MakeRosettesEmbed(dbUser);
            noneEmbed.Title = "🌾 Nothing to harvest";
            noneEmbed.Description = "None of your crops are ready yet.";
            noneEmbed.Color = FarmEngine.ErrorColor;
            await interaction.RespondAsync(embed: noneEmbed.Build(), ephemeral: true);
            return;
        }

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, except: "farm");

        int foundPet = await PetEngine.RollForPet(dbUser);
        if (foundPet > 0)
        {
            embed.AddField(
                "✨ You found a pet.",
                $"A friendly {PetEngine.PetNames(foundPet)} chased you about while you harvested. It's been added to your pets."
            );
            buttonRow.WithButton(label: "Pets", customId: "pets", style: ButtonStyle.Secondary, emote: new Emoji("🐾"));
            expIncrease = (expIncrease * 5) / 2;
        }

        if (plotsWereHarvested)
        {
            ActionRowBuilder actionRow = new();
            actionRow.WithButton(label: "Plant", customId: "crops_plant", style: ButtonStyle.Success, emote: new Emoji("🌱"));
            comps.AddRow(actionRow);
        }

        comps.AddRow(buttonRow);

        embed.Footer = new EmbedFooterBuilder
        {
            Text = $"{dbUser.AddExp(expIncrease)}  •  🌾 {count} {Pluralize(count, "plot", "plots")} harvested"
        };

        int damage = 3 + Global.Randomize(2);
        toolStatus -= damage;
        FarmEngine.ModifyItem(dbUser, "farmtools", -damage);

        if (toolStatus <= 0)
        {
            embed.AddField(
                $"🧰 {FarmEngine.GetItemName("farmtools")} destroyed",
                "Your tools broke while harvesting. Pick up a new set at the shop."
            );
        }

        await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
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
