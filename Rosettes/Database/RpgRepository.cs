using Dapper;
using Discord;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.RPG;

namespace Rosettes.Database
{
    public interface IRpgRepository
    {
        Task<int> FetchInventoryItem(User user, string item);
        Task<string> FetchInventoryStringItem(User user, string item);
        Task<IEnumerable<Crop>> GetUserCrops(IUser user);
        Task<bool> InsertCrop(Crop crop);
        Task<bool> ModifyInventoryItem(User user, string item, int amount);
        Task<bool> ModifyStrInventoryItem(User user, string item, string newValue);
        Task<bool> UpdateCrop(Crop crop);
    }

    public class RpgRepository : IRpgRepository
    {
        private static MySqlConnection DBConnection()
        {
            return new MySqlConnection(Settings.Database.ConnectionString);
        }

        public async Task<IEnumerable<Crop>> GetUserCrops(IUser user)
        {
            var db = DBConnection();

            var sql = @"SELECT plot_id, user_id, unix_growth, crop_type FROM users_crops WHERE user_id=@Id";

            return await db.QueryAsync<Crop>(sql, new { id = user.Id });
        }

        public async Task<bool> InsertCrop(Crop crop)
        {
            var db = DBConnection();

            var sql = @"INSERT INTO users_crops (plot_id, user_id, unix_growth, crop_type)
                        VALUES(@plotId, @userId, @unixGrowth, @cropType)";

            try
            {
                return (await db.ExecuteAsync(sql, new { crop.plotId, crop.userId, crop.unixGrowth, crop.cropType })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-insertcrop", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateCrop(Crop crop)
        {
            var db = DBConnection();

            var sql = @"UPDATE users_crops
                        SET plot_id=@plotId, user_id=@userId, unix_growth=@unixGrowth, crop_type=@cropType
                        WHERE id = @plotId AND user_id = @userId";

            try
            {
                return (await db.ExecuteAsync(sql, new { crop.plotId, crop.userId, crop.unixGrowth, crop.cropType })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-updateuser", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<int> FetchInventoryItem(User user, string item)
        {
            var db = DBConnection();

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

        public async Task<string> FetchInventoryStringItem(User user, string item)
        {
            var db = DBConnection();

            var sql = $"SELECT `{item}` FROM users_inventory WHERE id=@id";

            try
            {
                return await db.QueryFirstOrDefaultAsync<string>(sql, new { id = user.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getinventoryitem", $"sqlException code {ex.Message}");
                return "invalid";
            }
        }

        public async Task<bool> ModifyInventoryItem(User user, string item, int amount)
        {
            var db = DBConnection();

            var sql = $"UPDATE users_inventory SET {item} = {item} + @amount WHERE id=@id";

            try
            {
                return (await db.ExecuteAsync(sql, new { amount, id = user.Id })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-modifyinventory", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ModifyStrInventoryItem(User user, string item, string newValue)
        {
            var db = DBConnection();

            var sql = $"UPDATE users_inventory SET `{item}` = '{newValue}' WHERE id=@id";

            try
            {
                return (await db.ExecuteAsync(sql, new { id = user.Id })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-modifyinventory", $"sqlException code {ex.Message}");
                return false;
            }
        }
    }
}
