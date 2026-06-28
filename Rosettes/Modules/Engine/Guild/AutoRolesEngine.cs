using Dapper;
using Rosettes.Database;

namespace Rosettes.Modules.Engine.Guild;

public static class AutoRolesEngine
{
    private static List<AutoRoleEntry> _autoRolesEntries = [];
    private static List<AutoRoleGroup> _autoRolesGroups = [];
    private static readonly Lock ArCacheLock = new();

    public static ulong GetGuildIdFromMessage(ulong messageid)
    {
        lock (ArCacheLock)
        {
            return _autoRolesGroups.FirstOrDefault(x => x.MessageId == messageid)?.GuildId ?? 0;
        }
    }

    public static string GetNameFromCode(uint code)
    {
        lock (ArCacheLock)
        {
            return _autoRolesGroups.FirstOrDefault(x => x.Id == code)?.Name ?? "Autoroles.";
        }
    }

    public static IEnumerable<AutoRoleEntry> GetMessageRolesForEmote(ulong messageid, string emoteName)
    {
        lock (ArCacheLock)
        {
            uint? parentGroup = _autoRolesGroups.FirstOrDefault(x => x.MessageId == messageid)?.Id;
            if (parentGroup is null) return [];

            return _autoRolesEntries
                .Where(role => role.RoleGroupId == parentGroup.Value && role.Emote == emoteName)
                .ToList();
        }
    }

    public static IEnumerable<AutoRoleEntry>? GetRolesByCode(uint code, ulong guildid)
    {
        lock (ArCacheLock)
        {
            uint? parentGroup = _autoRolesGroups.FirstOrDefault(x => x.Id == code && x.GuildId == guildid)?.Id;
            if (parentGroup is null) return null;

            return _autoRolesEntries
                .Where(role => role.RoleGroupId == parentGroup.Value)
                .ToList();
        }
    }

    public static async Task<bool> SyncWithDatabase()
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        var sql = "SELECT guildid, emote, roleid, rolegroupid FROM autorole_entries";

        var newEntries = (await db.QueryAsync<AutoRoleEntry>(sql, new { })).ToList();

        sql = "SELECT id, guildid, messageid, name FROM autorole_groups";

        var newGroups = (await db.QueryAsync<AutoRoleGroup>(sql, new { })).ToList();

        lock (ArCacheLock)
        {
            _autoRolesEntries = newEntries;
            _autoRolesGroups = newGroups;
        }

        return true;
    }

    public static async Task<bool> ReloadGuildFromDatabase(ulong guildId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string entriesSql = "SELECT guildid, emote, roleid, rolegroupid FROM autorole_entries WHERE guildid=@GuildId";
        const string groupsSql = "SELECT id, guildid, messageid, name FROM autorole_groups WHERE guildid=@GuildId";

        var newEntries = (await db.QueryAsync<AutoRoleEntry>(entriesSql, new { GuildId = guildId })).ToList();
        var newGroups = (await db.QueryAsync<AutoRoleGroup>(groupsSql, new { GuildId = guildId })).ToList();

        lock (ArCacheLock)
        {
            _autoRolesEntries.RemoveAll(x => x.GuildId == guildId);
            _autoRolesEntries.AddRange(newEntries);

            _autoRolesGroups.RemoveAll(x => x.GuildId == guildId);
            _autoRolesGroups.AddRange(newGroups);
        }

        return true;
    }

    public static async Task<bool> UpdateGroupMessageId(uint code, ulong messageId)
    {
        lock (ArCacheLock)
        {
            var group = _autoRolesGroups.First(x => x.Id == code);
            group.MessageId = messageId;
        }

        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           UPDATE autorole_groups
                           SET messageid=@MessageId
                           WHERE id = @Code
                           """;

        await db.ExecuteAsync(sql, new { MessageId = messageId, Code = code });

        return true;
    }
}

public class AutoRoleEntry(ulong guildid, string emote, ulong roleid, uint rolegroupid)
{
    public ulong GuildId = guildid;
    public string Emote = emote;
    public ulong RoleId = roleid;
    public uint RoleGroupId = rolegroupid;
}

public class AutoRoleGroup(uint id, ulong guildid, ulong messageid, string name)
{
    public uint Id = id;
    public ulong GuildId = guildid;
    public ulong MessageId = messageid;
    public string Name = name;
}