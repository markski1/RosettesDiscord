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
            return await db.ExecuteAsync(sql, new { user.Id, Username = await user.GetUsername(), NameCache = await user.GetName(), user.MainPet }) > 0;
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
            return await db.ExecuteAsync(sql, new { user.Id, Username = await user.GetUsername(), NameCache = await user.GetName(), user.MainPet, user.Exp }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-updateuser", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<ulong?> GetUserByRosettesKey(string rosettesKey)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           SELECT id
                           FROM login_keys
                           WHERE login_key = @rosettes_key
                           LIMIT 1
                           """;

        try
        {
            return await db.QueryFirstOrDefaultAsync<ulong>(sql, new { rosettes_key = rosettesKey });
        }
        catch
        {
            return null;
        }
    }
}
