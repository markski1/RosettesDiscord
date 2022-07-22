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
        Task<bool> DeleteUser(User user);
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

            var sql = @"SELECT id, exp, currency, namecache FROM users";

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

            var sql = @"SELECT id, exp, currency, namecache FROM users WHERE id=@id";

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

            var sql = @"INSERT INTO users (id, exp, currency, namecache)
                        VALUES(@Id, @Exp, @Currency, @NameCache)";

            try
            {
                return (await db.ExecuteAsync(sql, new { user.Id, Exp = user.GetExperience(), Currency = user.GetCurrency(), NameCache = await user.GetName() })) > 0;
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
                        SET id=@Id, exp=@Exp, currency=@Currency, namecache=@NameCache
                        WHERE id = @Id";

            try
            {
                return (await db.ExecuteAsync(sql, new { user.Id, Exp = user.GetExperience(), Currency = user.GetCurrency(), NameCache = await user.GetName() })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-updateuser", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteUser(User user)
        {
            var db = DBConnection();

            var sql = @"DELETE FROM users
                        WHERE id = @Id";
            try
            {
                return (await db.ExecuteAsync(sql, new { user.Id })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-deleteuser", $"sqlException code {ex.Message}");
                return false;
            }
        }
    }
}
