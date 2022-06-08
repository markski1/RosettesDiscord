using Dapper;
using Discord;
using MySqlConnector;
using Rosettes.core;
using Rosettes.modules.engine;

namespace Rosettes.database
{
    public class UserRepository : IUserRepository
    {
        private static MySqlConnection DBConnection()
        {
            return new MySqlConnection(Settings.Database.ConnectionString);
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            var db = DBConnection();

            var sql = @"SELECT * FROM users";

            try
            {
                return await db.QueryAsync<User>(sql, new { });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getallusers", $"sqlException code {ex}");
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
                Global.GenerateErrorMessage("sql-checkuserexists", $"sqlException code {ex}");
                return false;
            }
}

        public async Task<User> GetUserData(IUser user)
        {
            var db = DBConnection();

            var sql = @"SELECT id, exp, currency FROM users WHERE id=@id";

            try
            {
                return await db.QueryFirstOrDefaultAsync<User>(sql, new { id = user.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getuserdata", $"sqlException code {ex}");
                return new User(0);
            }
        }

        public async Task<bool> InsertUser(User user)
        {
            var db = DBConnection();

            var sql = @"INSERT INTO users (id, exp, currency)
                        VALUES(@Id, @Exp, @Currency)";

            try
            {
                return (await db.ExecuteAsync(sql, new { user.Id, Exp = user.GetExperience(), Currency = user.GetCurrency() })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-insertuser", $"sqlException code {ex}");
                return false;
            }
        }

        public async Task<bool> UpdateUser(User user)
        {
            var db = DBConnection();

            var sql = @"UPDATE users
                        SET id=@Id, exp=@Exp, currency=@Currency
                        WHERE id = @Id";
            try
            {
                return (await db.ExecuteAsync(sql, new { user.Id, Exp = user.GetExperience(), Currency = user.GetCurrency() })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-updateuser", $"sqlException code {ex}");
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
                Global.GenerateErrorMessage("sql-deleteuser", $"sqlException code {ex}");
                return false;
            }
        }
    }
}
