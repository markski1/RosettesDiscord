using Dapper;
using Discord;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Database;

public static class AuthRepository
{
    public static async Task<ApplicationAuth?> GetApplicationAuth(string applicationKey)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT id, name, owner_id FROM app_auth WHERE key=@AppKey";

        try
        {
            return await db.QueryFirstOrDefaultAsync<ApplicationAuth>(sql, new { AppKey = applicationKey }) ?? null;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> RequestApplicationAuth(string applicationName, string actionName, User user)
    {
        EmbedBuilder embed = await Global.MakeRosettesEmbed();
        embed.Title = "Authorization Request";
        embed.Description = "An application is requesting you authorize an action. " +
                            "Please read carefully. Ignore or deny this request if you don't know what it is.";
        
        embed.AddField("Application", applicationName);
        embed.AddField("Action", actionName);

        ComponentBuilder comps = new ComponentBuilder();
        
        comps.WithButton("Accept", $"accept:{applicationName}:{actionName}", ButtonStyle.Success);
        comps.WithButton("Deny", $"deny:{applicationName}:{actionName}", ButtonStyle.Danger);

        var userRef = await UserEngine.GetUserReferenceById(user.Id);

        if (userRef is null)
        {
            return false;
        }

        try
        {
            await userRef.SendMessageAsync(embed: embed.Build(), components: comps.Build());
        }
        catch
        {
            // Most likely due to missing perms. User does not allow DMs or is no longer in a server w/ us.
            return false;
        }

        return true;
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

        const string sql = "SELECT app_id, user_id FROM app_auth_rel WHERE app_id=@AppId and user_id=@UserId";

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
