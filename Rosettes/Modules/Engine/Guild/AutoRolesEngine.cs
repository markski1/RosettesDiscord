using Dapper;
using Rosettes.Database;

namespace Rosettes.Modules.Engine.Guild;

public static class AutoRolesEngine
{
    private static List<AutoRoleEntry> _autoRolesEntries = [];
    private static List<AutoRoleGroup> _autoRolesGroups = [];

    public static ulong GetGuildIdFromMessage(ulong messageid)
    {
        try
        {
            return _autoRolesGroups.First(x => x.MessageId == messageid).GuildId;
        }
        catch
        {
            return 0;
        }
    }

    public static string GetNameFromCode(uint code)
    {
        try
        {
            return _autoRolesGroups.First(x => x.Id == code).Name;
        }
        catch
        {
            return "Autoroles.";
        }
    }

    public static IEnumerable<AutoRoleEntry> GetMessageRolesForEmote(ulong messageid, string emoteName)
    {
        uint parentGroup = _autoRolesGroups.First(x => x.MessageId == messageid).Id;
        IEnumerable<AutoRoleEntry> foundEntries = from role in _autoRolesEntries
            where role.RoleGroupId == parentGroup && role.Emote == emoteName
            select role;
        return foundEntries;
    }

    public static IEnumerable<AutoRoleEntry>? GetRolesByCode(uint code, ulong guildid)
    {
        IEnumerable<AutoRoleEntry>? foundEntries;
        try
        {
            uint parentGroup = _autoRolesGroups.First(x => x.Id == code && x.GuildId == guildid).Id;
            foundEntries =
                from role in _autoRolesEntries
                where role.RoleGroupId == parentGroup
                select role;
        }
        catch
        {
            foundEntries = null;
        }
        return foundEntries;
    }

    public static async Task<bool> SyncWithDatabase()
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        var sql = "SELECT guildid, emote, roleid, rolegroupid FROM autorole_entries";

        _autoRolesEntries = (await db.QueryAsync<AutoRoleEntry>(sql, new { })).ToList();

        sql = "SELECT id, guildid, messageid, name FROM autorole_groups";

        _autoRolesGroups = (await db.QueryAsync<AutoRoleGroup>(sql, new { })).ToList();

        return true;
    }

    public static async Task<bool> UpdateGroupMessageId(uint code, ulong messageId)
    {
        var group = _autoRolesGroups.First(x => x.Id == code);

        group.MessageId = messageId;

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