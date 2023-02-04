using Dapper;
using Discord;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Database
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User> GetUserData(IUser user);
        Task<bool> CheckUserExists(IUser user);
        Task<bool> InsertUser(User user);
        Task<bool> UpdateUser(User user);
        Task<int> FetchInventoryItem(User user, string item);
        Task<string> FetchInventoryStringItem(User user, string item);
        Task<bool> ModifyInventoryItem(User user, string item, int amount);
        Task<bool> ModifyStrInventoryItem(User user, string item, string newValue);
    }

    public class UserRepository : IUserRepository
    {
        private static MySqlConnection DBConnection()
        {
            return new MySqlConnection(Settings.Database.ConnectionString);
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            var db = DBConnection();

            var sql = @"SELECT id, namecache, exp, mainpet FROM users";

            try
            {
                return await db.QueryAsync<User>(sql, new { });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getallusers", $"sqlException code {ex.Message}");
                return new List<User>();
            }
        }
        
        public async Task<bool> CheckUserExists(IUser user)
        {
            var db = DBConnection();

            var sql = @"SELECT count(1) FROM users WHERE id=@Id";

            try
            {
                return await db.ExecuteScalarAsync<bool>(sql, new { user.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-checkuserexists", $"sqlException code {ex.Message}");
                return false;
            }
}

        public async Task<User> GetUserData(IUser user)
        {
            var db = DBConnection();

            var sql = @"SELECT id, namecache, exp, mainpet FROM users WHERE id=@id";

            try
            {
                return await db.QueryFirstOrDefaultAsync<User>(sql, new { id = user.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getuserdata", $"sqlException code {ex.Message}");
                return new User(null);
            }
        }

        public async Task<bool> InsertUser(User user)
        {
            var db = DBConnection();

            var sql = @"INSERT INTO users (id, namecache, mainpet)
                        VALUES(@Id, @NameCache, @MainPet)";

            try
            {
                return (await db.ExecuteAsync(sql, new { user.Id, NameCache = await user.GetName(), user.MainPet })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-insertuser", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateUser(User user)
        {
            var db = DBConnection();

            var sql = @"UPDATE users
                        SET id=@Id, namecache=@NameCache, mainpet=@MainPet, exp=@Exp
                        WHERE id = @Id";

            try
            {
                return (await db.ExecuteAsync(sql, new { user.Id, NameCache = await user.GetName(), user.MainPet, user.Exp })) > 0;
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
