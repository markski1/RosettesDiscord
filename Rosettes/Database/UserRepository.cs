using System.Security.Cryptography;
using System.Text;
using Dapper;
using Discord;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Database;

public class UserRepository
{
    public static async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT id, username, namecache, exp, mainpet FROM users";

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

    public static async Task<bool> CheckUserExists(IUser user)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT count(1) FROM users WHERE id=@Id";

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

    public static async Task<User> GetUserData(IUser user)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT id, username, namecache, exp, mainpet FROM users WHERE id=@id";

        try
        {
            return await db.QueryFirstOrDefaultAsync<User>(sql, new { id = user.Id }) ?? new User(null);
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getuserdata", $"sqlException code {ex.Message}");
            return new User(null);
        }
    }

    public static async Task<bool> InsertUser(User user)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           INSERT INTO users (id, username, namecache, mainpet)
                           VALUES(@Id, @Username, @NameCache, @MainPet)
                           """;

        const string sql2 = "INSERT INTO users_inventory (id) VALUES(@Id)";

        try
        {
            await db.ExecuteAsync(sql2, new { user.Id });
            return await db.ExecuteAsync(sql, new { user.Id, user.Username, user.NameCache, user.MainPet }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-insertuser", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> UpdateUser(User user)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           UPDATE users
                           SET id=@Id, username=@Username, namecache=@NameCache, mainpet=@MainPet, exp=@Exp
                           WHERE id = @Id
                           """;

        try
        {
            return await db.ExecuteAsync(sql, new { user.Id, user.Username, user.NameCache, user.MainPet, user.Exp }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-updateuser", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static string HashRosettesKey(string rosettesKey)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rosettesKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static async Task<ulong?> GetUserByRosettesKey(string rosettesKey)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           SELECT id
                           FROM login_keys
                           WHERE login_key_hash = @KeyHash OR login_key = @PlainKey
                           LIMIT 1
                           """;

        try
        {
            ulong? userId = await db.QueryFirstOrDefaultAsync<ulong?>(sql, new
            {
                KeyHash = HashRosettesKey(rosettesKey),
                PlainKey = rosettesKey
            });

            if (userId is not null && userId != 0)
            {
                await db.ExecuteAsync("UPDATE login_keys SET last_used_at=UTC_TIMESTAMP() WHERE id=@Id", new { Id = userId.Value });
            }

            return userId;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getuserbyrosetteskey", $"sqlException code {ex.Message}");
            throw;
        }
    }
}
