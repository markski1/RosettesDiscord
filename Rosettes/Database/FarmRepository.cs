using Dapper;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Minigame.Farming;

namespace Rosettes.Database;

public static class FarmRepository
{
    public static async Task<IEnumerable<Crop>> GetUserCrops(User user)
    {
        using var db = DatabasePool.GetConnection();

        const string sql = "SELECT plot_id, user_id, unix_growth, unix_next_water, crop_type FROM users_crops WHERE user_id=@Id";

        return await db.QueryAsync<Crop>(sql, new { id = user.Id });
    }

    public static async Task<bool> ApplyWateringResults(IReadOnlyCollection<Crop> wateredCrops)
    {
        if (wateredCrops.Count == 0) return true;

        using var db = DatabasePool.GetConnection();

        if (db.State == System.Data.ConnectionState.Closed)
        {
            await db.OpenAsync();
        }

        await using var transaction = await db.BeginTransactionAsync();
        const string sql = """
                           UPDATE users_crops
                           SET unix_growth=@UnixGrowth, unix_next_water=@UnixNextWater
                           WHERE plot_id=@PlotId AND user_id=@UserId
                           """;

        try
        {
            var parameters = wateredCrops.Select(crop => new
            {
                crop.UnixGrowth,
                crop.UnixNextWater,
                crop.PlotId,
                crop.UserId
            });
            int updated = await db.ExecuteAsync(sql, parameters, transaction);

            if (updated != wateredCrops.Count)
            {
                await transaction.RollbackAsync();
                return false;
            }

            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(); } catch { }
            Global.GenerateErrorMessage("sql-applywatering", $"Transaction failed: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> ApplyPlantingResults(User user, IReadOnlyCollection<Crop> plantedCrops, int seedsUsed, int toolDamage)
    {
        using var db = DatabasePool.GetConnection();

        if (db.State == System.Data.ConnectionState.Closed)
        {
            db.Open();
        }

        await using var transaction = await db.BeginTransactionAsync();

        const string insertCropSql = """
                                     INSERT INTO users_crops (plot_id, user_id, unix_growth, unix_next_water, crop_type)
                                     VALUES(@plotId, @userId, @unixGrowth, @unixNextWater, @cropType)
                                     """;

        const string updateSeedSql = "UPDATE users_inventory SET seedbag = seedbag - @seedsUsed WHERE id = @id";
        const string updateToolsSql = "UPDATE users_inventory SET farmtools = farmtools - @toolDamage WHERE id = @id";

        try
        {
            foreach (var crop in plantedCrops)
            {
                await db.ExecuteAsync(insertCropSql, new
                {
                    plotId = crop.PlotId,
                    userId = crop.UserId,
                    unixGrowth = crop.UnixGrowth,
                    unixNextWater = crop.UnixNextWater,
                    cropType = crop.CropType
                }, transaction);
            }

            if (seedsUsed > 0)
                await db.ExecuteAsync(updateSeedSql, new { seedsUsed, id = user.Id }, transaction);

            if (toolDamage > 0)
                await db.ExecuteAsync(updateToolsSql, new { toolDamage, id = user.Id }, transaction);

            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Global.GenerateErrorMessage("sql-applyplanting", $"Transaction failed: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> ApplyHarvestResults(User user, IReadOnlyCollection<Crop> harvestedCrops, IReadOnlyDictionary<string, int> rewards, int toolDamage,
        int degradedPlotMask)
    {
        foreach (var reward in rewards)
        {
            if (!FarmEngine.IsValidItem(reward.Key)) return false;
        }

        using var db = DatabasePool.GetConnection();

        if (db.State == System.Data.ConnectionState.Closed)
        {
            db.Open();
        }

        await using var transaction = await db.BeginTransactionAsync();

        const string deleteCropSql = """
                                     DELETE FROM users_crops
                                     WHERE user_id = @userId AND plot_id = @plotId
                                     """;
        const string updateToolsSql = "UPDATE users_inventory SET farmtools = farmtools - @toolDamage WHERE id = @id";
        const string updateDegradedPlotsSql = "UPDATE users_inventory SET plots_degraded = plots_degraded | @degradedPlotMask WHERE id = @id";

        try
        {
            foreach (var crop in harvestedCrops)
            {
                int deleted = await db.ExecuteAsync(
                    deleteCropSql,
                    new { userId = crop.UserId, plotId = crop.PlotId },
                    transaction);

                if (deleted != 1)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            foreach (var reward in rewards.Where(r => r.Value != 0))
            {
                var updateRewardSql = $"UPDATE users_inventory SET `{reward.Key}` = `{reward.Key}` + @amount WHERE id = @id";
                await db.ExecuteAsync(updateRewardSql, new { amount = reward.Value, id = user.Id }, transaction);
            }

            if (toolDamage > 0)
                await db.ExecuteAsync(updateToolsSql, new { toolDamage, id = user.Id }, transaction);

            if (degradedPlotMask != 0)
                await db.ExecuteAsync(updateDegradedPlotsSql, new { degradedPlotMask, id = user.Id }, transaction);

            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Global.GenerateErrorMessage("sql-applyharvest", $"Transaction failed: {ex.Message}");
            return false;
        }
    }

    public static async Task<int> FetchInventoryItem(User user, string item)
    {
        if (!FarmEngine.IsValidItem(item))
            throw new ArgumentException($"Unknown inventory item: {item}", nameof(item));

        using var db = DatabasePool.GetConnection();

        var sql = $"SELECT `{item}` FROM users_inventory WHERE id=@id";

        try
        {
            return await db.QueryFirstOrDefaultAsync<int>(sql, new { id = user.Id });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getinventoryitem", $"sqlException code {ex.Message}");
            throw;
        }
    }

    public static async Task<bool> TryConsumeInventoryItem(User user, string item, int amount)
    {
        if (!FarmEngine.IsValidItem(item) || amount <= 0) return false;

        using var db = DatabasePool.GetConnection();

        string sql = $"""
                     UPDATE users_inventory
                     SET `{item}` = `{item}` - @amount
                     WHERE id=@id AND `{item}` >= @amount
                     """;

        try
        {
            return await db.ExecuteAsync(sql, new { amount, id = user.Id }) == 1;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-consumeinventory", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> ApplyFishingResults(User user, string caughtItem, int toolDamage)
    {
        if (!FarmEngine.IsValidItem(caughtItem) || caughtItem == "dabloons" || toolDamage <= 0)
            return false;

        using var db = DatabasePool.GetConnection();

        string sql = $"""
                     UPDATE users_inventory
                     SET `{caughtItem}` = `{caughtItem}` + 1,
                         fishpole = fishpole - @toolDamage
                     WHERE id=@id AND fishpole > 0
                     """;

        try
        {
            return await db.ExecuteAsync(sql, new { toolDamage, id = user.Id }) == 1;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-applyfishing", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> TryPurchaseInventoryItem(
        User user,
        string item,
        int amount,
        int cost,
        bool replaceItem = false,
        int? maximumCurrentValue = null)
    {
        if (!FarmEngine.IsValidItem(item) || item == "dabloons" || amount <= 0 || cost <= 0)
            return false;

        using var db = DatabasePool.GetConnection();

        string itemUpdate = replaceItem
            ? $"`{item}` = @amount"
            : $"`{item}` = `{item}` + @amount";
        string maximumCondition = maximumCurrentValue.HasValue
            ? $" AND `{item}` < @maximumCurrentValue"
            : "";
        string sql = $"""
                     UPDATE users_inventory
                     SET dabloons = dabloons - @cost, {itemUpdate}
                     WHERE id = @id AND dabloons >= @cost{maximumCondition}
                     """;

        try
        {
            return await db.ExecuteAsync(sql, new
            {
                amount,
                cost,
                id = user.Id,
                maximumCurrentValue
            }) == 1;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-purchaseitem", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> TrySellInventoryItem(User user, string item, int amount, int proceeds)
    {
        if (!FarmEngine.IsValidItem(item) || item == "dabloons" || amount <= 0 || proceeds <= 0)
            return false;

        using var db = DatabasePool.GetConnection();

        string sql = $"""
                     UPDATE users_inventory
                     SET `{item}` = `{item}` - @amount, dabloons = dabloons + @proceeds
                     WHERE id = @id AND `{item}` >= @amount
                     """;

        try
        {
            return await db.ExecuteAsync(sql, new { amount, proceeds, id = user.Id }) == 1;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-sellitem", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> TryRestorePlots(User user, int cost)
    {
        if (cost <= 0) return false;

        using var db = DatabasePool.GetConnection();

        const string sql = """
                           UPDATE users_inventory
                           SET dabloons = dabloons - @cost, plots_degraded = 0
                           WHERE id = @id AND dabloons >= @cost AND plots_degraded != 0
                           """;

        try
        {
            return await db.ExecuteAsync(sql, new { cost, id = user.Id }) == 1;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-restoreplots", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SetInventoryItem(User user, string item, int newValue)
    {
        if (!FarmEngine.IsValidItem(item)) return false;

        using var db = DatabasePool.GetConnection();

        var sql = $"UPDATE users_inventory SET {item} = @newValue WHERE id=@id";

        try
        {
            return await db.ExecuteAsync(sql, new { newValue, id = user.Id }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-modifyinventory", $"sqlException code {ex.Message}");
            return false;
        }
    }
}
