using Dapper;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Minigame.Farming;

namespace Rosettes.Database;

public static class FarmRepository
{
    public static async Task<IEnumerable<Crop>> GetUserCrops(User user)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT plot_id, user_id, unix_growth, unix_next_water, crop_type FROM users_crops WHERE user_id=@Id";

        return await db.QueryAsync<Crop>(sql, new { id = user.Id });
    }

    public static async Task<bool> InsertCrop(Crop crop)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           INSERT INTO users_crops (plot_id, user_id, unix_growth, unix_next_water, crop_type)
                           VALUES(@plotId, @userId, @unixGrowth, @unixNextWater, @cropType)
                           """;

        try
        {
            return await db.ExecuteAsync(sql, new { plotId = crop.PlotId, userId = crop.UserId, unixGrowth = crop.UnixGrowth, unixNextWater = crop.UnixNextWater,
                cropType = crop.CropType }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-insertcrop", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> UpdateCrop(Crop crop)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           UPDATE users_crops
                           SET plot_id=@plotId, user_id=@userId, unix_growth=@unixGrowth, unix_next_water=@unixNextWater, crop_type=@cropType
                           WHERE plot_id = @plotId AND user_id = @userId
                           """;

        try
        {
            return await db.ExecuteAsync(sql, new { plotId = crop.PlotId, userId = crop.UserId, unixGrowth = crop.UnixGrowth, unixNextWater = crop.UnixNextWater,
                cropType = crop.CropType }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-updatecrop", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> DeleteCrop(Crop crop)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           DELETE FROM users_crops
                           WHERE user_id = @userId AND plot_id = @plotId
                           """;
        try
        {
            return await db.ExecuteAsync(sql, new { userId = crop.UserId, plotId = crop.PlotId }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-deletecrop", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<int> FetchInventoryItem(User user, string item)
    {
        if (!FarmEngine.IsValidItem(item)) return -1;

        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        var sql = $"SELECT `{item}` FROM users_inventory WHERE id=@id";

        try
        {
            return await db.QueryFirstOrDefaultAsync<int>(sql, new { id = user.Id });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getinventoryitem", $"sqlException code {ex.Message}");
            return -1;
        }
    }

    public static async Task<bool> ModifyInventoryItem(User user, string item, int amount)
    {
        if (!FarmEngine.IsValidItem(item)) return false;

        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        // 
        var sql = $"UPDATE users_inventory SET {item} = {item} + @amount WHERE id=@id";

        try
        {
            return await db.ExecuteAsync(sql, new { amount, id = user.Id }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-modifyinventory", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SetInventoryItem(User user, string item, int newValue)
    {
        if (!FarmEngine.IsValidItem(item)) return false;

        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

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
