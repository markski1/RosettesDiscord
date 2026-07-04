using System.Security.Cryptography;
using System.Text;
using Dapper;
using Rosettes.Modules.Engine;

namespace Rosettes.Database;

public static class AuthRepository
{
    public static string HashToken(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static async Task<ApplicationAuth?> GetApplicationAuth(string applicationKey)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
            SELECT
                id,
                name,
                owner_id AS OwnerId,
                created_at AS CreatedAt,
                last_used_at AS LastUsedAt,
                last_rotated_at AS LastRotatedAt
            FROM app_auth
            WHERE token_hash=@TokenHash OR token_key=@AppKey
            """;

        try
        {
            var app = await db.QueryFirstOrDefaultAsync<ApplicationAuth>(sql, new
            {
                TokenHash = HashToken(applicationKey),
                AppKey = applicationKey
            });

            if (app is not null)
            {
                await db.ExecuteAsync("UPDATE app_auth SET last_used_at=UTC_TIMESTAMP() WHERE id=@Id", new { app.Id });
            }

            return app;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<int?> CreateApplication(string name, ulong ownerId, string token)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
            INSERT INTO app_auth (name, owner_id, token_key, token_hash, created_at, last_rotated_at)
            VALUES (@Name, @OwnerId, NULL, @TokenHash, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;

        try
        {
            return await db.ExecuteScalarAsync<int>(sql, new
            {
                Name = name,
                OwnerId = ownerId,
                TokenHash = HashToken(token)
            });
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> RotateApplicationToken(int appId, ulong ownerId, string token)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
            UPDATE app_auth
            SET token_key=NULL, token_hash=@TokenHash, last_rotated_at=UTC_TIMESTAMP()
            WHERE id=@AppId AND owner_id=@OwnerId
            """;

        try
        {
            return await db.ExecuteAsync(sql, new
            {
                AppId = appId,
                OwnerId = ownerId,
                TokenHash = HashToken(token)
            }) > 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> DeleteApplication(int appId, ulong ownerId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;
        using var tx = await db.BeginTransactionAsync();

        try
        {
            int deleted = await db.ExecuteAsync(
                "DELETE FROM app_auth WHERE id=@AppId AND owner_id=@OwnerId",
                new { AppId = appId, OwnerId = ownerId },
                tx);

            if (deleted == 0)
            {
                await tx.RollbackAsync();
                return false;
            }

            await db.ExecuteAsync(
                "DELETE FROM app_auth_rel WHERE app_id=@AppId",
                new { AppId = appId },
                tx);

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            return false;
        }
    }

    public static async Task<bool> RevokeApplicationUser(int appId, ulong ownerId, ulong userId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
            DELETE r
            FROM app_auth_rel r
            INNER JOIN app_auth a ON a.id = r.app_id
            WHERE r.app_id=@AppId AND r.user_id=@UserId AND a.owner_id=@OwnerId
            """;

        try
        {
            return await db.ExecuteAsync(sql, new { AppId = appId, OwnerId = ownerId, UserId = userId }) > 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> AuthUser(int appId, ulong userId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "INSERT INTO app_auth_rel (user_id, app_id) VALUES(@UserId, @AppId)";

        try
        {
            return await db.ExecuteAsync(sql, new { UserId = userId, AppId = appId }) > 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<ApplicationRelation?> GetApplicationRelation(string applicationKey, ulong userId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        var appAuth = await GetApplicationAuth(applicationKey);

        if (appAuth is null)
        {
            return null;
        }

        const string sql = "SELECT app_id AS ApplicationId, user_id AS UserId FROM app_auth_rel WHERE app_id=@AppId and user_id=@UserId";

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
