using Dapper;
using Rosettes.Modules.Engine;

namespace Rosettes.Database;

public class AuthRepository
{
    public async Task<ApplicationAuth?> GetApplicationAuth(string applicationKey)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"SELECT id, name, owner_id FROM app_auth WHERE key=@AppKey";

        try
        {
            return await db.QueryFirstOrDefaultAsync<ApplicationAuth>(sql, new { AppKey = applicationKey }) ?? null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApplicationRelation?> GetApplicationRelation(string applicationKey, ulong userId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var appAuth = GetApplicationAuth(applicationKey);

        if (appAuth == null)
        {
            return null;
        }

        var sql = @"SELECT app_id, user_id FROM app_auth_rel WHERE app_id=@AppId and user_id=@UserId";

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
