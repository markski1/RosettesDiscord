using Dapper;
using Rosettes.Modules.Engine;

namespace Rosettes.Database;

public static class AuthRepository
{
    public static async Task<ApplicationAuth?> GetApplicationAuth(string applicationKey)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        const string sql = @"SELECT id, name, owner_id FROM app_auth WHERE key=@AppKey";

        try
        {
            return await db.QueryFirstOrDefaultAsync<ApplicationAuth>(sql, new { AppKey = applicationKey }) ?? null;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> AuthUser(int appId, ulong userId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        const string sql = "INSERT INTO app_auth_rel (user_id, app_id) VALUES(@UserId, @AppId)";

        try
        {
            return (await db.ExecuteAsync(sql, new { UserId = userId, AppId = appId })) > 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<ApplicationRelation?> GetApplicationRelation(string applicationKey, ulong userId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var appAuth = await GetApplicationAuth(applicationKey);

        if (appAuth is null)
        {
            return null;
        }

        const string sql = @"SELECT app_id, user_id FROM app_auth_rel WHERE app_id=@AppId and user_id=@UserId";

        try
        {
            return await db.QueryFirstOrDefaultAsync<ApplicationRelation>(sql, new { AppId = appAuth.Id, UserId = userId }) ?? null;
        }
        catch
        {
            return null;
        }
    }
}
