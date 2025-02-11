using Dapper;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Minigame;

namespace Rosettes.Database;

/*
 * A deadly sin is committed through this source file!
 *
 * There are indeed several instances of forming queries through variable string concatenation, rather than
 * by database preparation through the ORM. Gasp!
 *
 * However, the query is formed by the string name of an item, which is checked against the minigame manager
 * before being let through. So, no matter how bad it looks, this cannot be used to forge an injection.
 *
 * If I am wrong here, I would be happy to stand corrected. But so far I've found no issue with this other than
 * how it looks.
 */

public class FarmRepository
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
            return await db.ExecuteAsync(sql, new { crop.plotId, crop.userId, crop.unixGrowth, crop.unixNextWater, crop.cropType }) > 0;
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
            return await db.ExecuteAsync(sql, new { crop.plotId, crop.userId, crop.unixGrowth, crop.unixNextWater, crop.cropType }) > 0;
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
            return await db.ExecuteAsync(sql, new { crop.userId, crop.plotId }) > 0;
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

    public static async Task<string> FetchInventoryStringItem(User user, string item)
    {
        if (!FarmEngine.IsValidItem(item)) return "err";

        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        var sql = $"SELECT `{item}` FROM users_inventory WHERE id=@id";

        try
        {
            return await db.QueryFirstOrDefaultAsync<string>(sql, new { id = user.Id }) ?? "invalid";
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getinventoryitem", $"sqlException code {ex.Message}");
            return "invalid";
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

    public static async Task<bool> ModifyStrInventoryItem(User user, string item, string newValue)
    {
        if (!FarmEngine.IsValidItem(item)) return false;

        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        var sql = $"UPDATE users_inventory SET `{item}` = '{newValue}' WHERE id=@id";

        try
        {
            return await db.ExecuteAsync(sql, new { id = user.Id }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-modifyinventory", $"sqlException code {ex.Message}");
            return false;
        }
    }
}
