using System.Collections.Concurrent;
using System.Text;
using Dapper;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Engine.Guild;

namespace Rosettes.Database;

public class GuildRepository
{
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> RoleSyncLocks = []; 
    public static async Task<IEnumerable<Guild>> GetAllGuildsAsync()
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT id, namecache, members, settings, ownerid, defaultrole, logchan, rpgchan FROM guilds";

        try
        {
            return await db.QueryAsync<Guild>(sql, new { });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getallguilds", $"sqlException code {ex.Message}");
            return new List<Guild>();
        }
    }

    public static async Task<bool> CheckGuildExists(ulong guildId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT count(1) FROM guilds WHERE id=@guildId";

        try
        {
            return await db.ExecuteScalarAsync<bool>(sql, new { guildId });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-checkguildexists", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<Guild> GetGuildData(SocketGuild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT id, namecache, members, settings, ownerid, defaultrole, logchan, rpgchan FROM guilds WHERE id=@id";

        try
        {
            return await db.QueryFirstOrDefaultAsync<Guild>(sql, new { id = guild.Id }) ?? new Guild(null);
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getguilddata", $"sqlException code {ex.Message}");
            return new Guild(null);
        }
    }

    public static async Task<string> GetGuildSettings(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT settings FROM guilds WHERE id=@id";

        try
        {
            return await db.QueryFirstOrDefaultAsync<string>(sql, new { id = guild.Id }) ?? "1111111111";
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getguildsettings", $"sqlException code {ex.Message}");
            return "1111111111";
        }
    }

    public static async Task<bool> SetGuildSettings(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           UPDATE guilds
                           SET settings = @Settings
                           WHERE id = @Id
                           """;

        try
        {
            return await db.ExecuteAsync(sql, new { guild.Settings, guild.Id }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-updateguild", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<ulong> GetGuildDefaultRole(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT defaultrole FROM guilds WHERE id=@id";

        try
        {
            return await db.QueryFirstOrDefaultAsync<ulong>(sql, new { id = guild.Id });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getguildsettings", $"sqlException code {ex.Message}");
            return 0;
        }
    }

    public static async Task<(string settings, ulong defaultRole, ulong logChannel, ulong farmChannel)?> GetGuildRuntimeFields(ulong guildId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT settings, defaultrole, logchan, rpgchan FROM guilds WHERE id=@id";

        try
        {
            var row = await db.QueryFirstOrDefaultAsync(sql, new { id = guildId });
            if (row is null) return null;
            return ((string)row.settings, (ulong)row.defaultrole, (ulong)row.logchan, (ulong)row.rpgchan);
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getguildruntimefields", $"sqlException code {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> InsertGuild(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           INSERT INTO guilds (id, namecache, members, settings, ownerid)
                           VALUES(@Id, @NameCache, @Members, @Settings, @OwnerId)
                           """;

        try
        {
            return await db.ExecuteAsync(sql, new { guild.Id, guild.NameCache, guild.Members, guild.Settings, guild.OwnerId }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-insertguild", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> UpdateGuild(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           UPDATE guilds
                           SET id=@Id, namecache=@NameCache, members=@Members, ownerid=@OwnerId, logchan=@LogChannel, rpgchan=@FarmChannel
                           WHERE id = @Id
                           """;
        try
        {
            return await db.ExecuteAsync(sql, new { guild.Id, guild.NameCache, guild.Members, guild.Settings, guild.OwnerId, guild.LogChannel, guild.FarmChannel }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-updateguild", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> UpdateGuildRoles(Guild guild)
    {
        var discordGuild = guild.GetDiscordSocketReference();
        if (discordGuild is null) return false;

        var roleSyncLock = RoleSyncLocks.GetOrAdd(guild.Id, _ => new SemaphoreSlim(1, 1));
        await roleSyncLock.WaitAsync();
        try
        {
            using var getConn = DatabasePool.GetConnection();
            var db = getConn.Db;
            var roles = discordGuild.Roles.Where(r => !r.IsEveryone && !r.IsManaged).ToList();

            if (db.State == System.Data.ConnectionState.Closed)
            {
                db.Open();
            }

            await using var transaction = await db.BeginTransactionAsync();
            try
            {
                await db.ExecuteAsync("DELETE FROM roles WHERE guildid = @Id", new { guild.Id }, transaction);

                if (roles.Count > 0)
                {
                    var sql = new StringBuilder("INSERT INTO roles (id, rolename, guildid, color) VALUES ");
                    var parameters = new DynamicParameters();
                    parameters.Add("GuildId", guild.Id);

                    for (int i = 0; i < roles.Count; i++)
                    {
                        if (i > 0) sql.Append(", ");
                        sql.Append($"(@Id{i}, @Name{i}, @GuildId, @Color{i})");
                        parameters.Add($"Id{i}", roles[i].Id);
                        parameters.Add($"Name{i}", roles[i].Name);
                        parameters.Add($"Color{i}", roles[i].Colors.PrimaryColor.ToString());
                    }

                    await db.ExecuteAsync(sql.ToString(), parameters, transaction);
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Global.GenerateErrorMessage("sql-updateguildroles", $"Transaction failed: {ex.Message}");
                return false;
            }
        }
        finally
        {
            roleSyncLock.Release();
        }
    }
}
