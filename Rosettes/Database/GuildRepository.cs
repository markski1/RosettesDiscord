using Dapper;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Engine.Guild;

namespace Rosettes.Database;

public class GuildRepository
{
    public async Task<IEnumerable<Guild>> GetAllGuildsAsync()
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"SELECT id, namecache, members, settings, ownerid, defaultrole, logchan, rpgchan FROM guilds";

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

    public async Task<bool> CheckGuildExists(ulong guildId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"SELECT count(1) FROM guilds WHERE id=@guildId";

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

    public async Task<Guild> GetGuildData(SocketGuild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"SELECT id, namecache, members, settings, ownerid, defaultrole, logchan, rpgchan FROM guilds WHERE id=@id";

        try
        {
            return (await db.QueryFirstOrDefaultAsync<Guild>(sql, new { id = guild.Id })) ?? new Guild(null);
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getguilddata", $"sqlException code {ex.Message}");
            return new Guild(null);
        }
    }

    public async Task<string> GetGuildSettings(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"SELECT settings FROM guilds WHERE id=@id";

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

    public async Task<bool> SetGuildSettings(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"UPDATE guilds
                        SET settings = @Settings
                        WHERE id = @Id";

        try
        {
            return (await db.ExecuteAsync(sql, new { guild.Settings, guild.Id })) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-updateguild", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public async Task<ulong> GetGuildDefaultRole(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"SELECT defaultrole FROM guilds WHERE id=@id";

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

    public async Task<bool> InsertGuild(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"INSERT INTO guilds (id, namecache, members, settings, ownerid)
                        VALUES(@Id, @NameCache, @Members, @Settings, @OwnerId)";

        try
        {
            return (await db.ExecuteAsync(sql, new { guild.Id, guild.NameCache, guild.Members, guild.Settings, guild.OwnerId })) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-insertguild", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateGuild(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"UPDATE guilds
                        SET id=@Id, namecache=@NameCache, members=@Members, ownerid=@OwnerId, logchan=@LogChannel, rpgchan=@FarmChannel
                        WHERE id = @Id";
        try
        {
            return (await db.ExecuteAsync(sql, new { guild.Id, guild.NameCache, guild.Members, guild.Settings, guild.OwnerId, guild.LogChannel, guild.FarmChannel })) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-updateguild", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateGuildRoles(Guild guild)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var discordGuild = guild.GetDiscordSocketReference();
        if (discordGuild is null) return false;

        var sql = @"DELETE FROM roles WHERE guildid = @Id";
        await db.ExecuteAsync(sql, new { guild.Id });

        sql = @"INSERT INTO roles (id, rolename, guildid, color)
                    VALUES (@Id, @Name, @GuildId, @Color)";

        var sql2 = @"UPDATE roles
                        SET rolename=@Name, guildid=@GuildId, color=@Color
                        WHERE id = @Id";

        foreach (var role in discordGuild.Roles)
        {
            if (role.IsEveryone) continue;
            if (role.IsManaged) continue;
            try
            {
                await db.ExecuteAsync(sql, new { role.Id, role.Name, GuildId = guild.Id, Color = role.Color.ToString() });
            }
            catch
            {
                // if can't insert, attempt to update
                try
                {
                    await db.ExecuteAsync(sql2, new { role.Id, role.Name, GuildId = guild.Id, Color = role.Color.ToString() });
                }
                catch
                {
                    // if we also failed to update, just fail.
                }
            }
        }

        return true;
    }
}
