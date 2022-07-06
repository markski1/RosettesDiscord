using Dapper;
using MySqlConnector;
using Rosettes.Core;

namespace Rosettes.Modules.Engine
{
    public static class AutoRolesEngine
    {
        private static List<AutoRoleEntry> AutoRolesEntries = new();

        public static IEnumerable<AutoRoleEntry> GetGuildRolesForEmote(ulong guildid, string emoteName)
        {
            IEnumerable<AutoRoleEntry> FoundEntries;
            FoundEntries =
                from role in AutoRolesEntries
                where role.GuildId == guildid && role.Emote == emoteName
                select role;
            return FoundEntries;
        }

        public static IEnumerable<AutoRoleEntry> GetGuildAutoroles(ulong guildid)
        {
            IEnumerable<AutoRoleEntry> FoundEntries;
            FoundEntries =
                from role in AutoRolesEntries
                where role.GuildId == guildid
                select role;
            return FoundEntries;
        }

        public static async Task<bool> SyncWithDatabase()
           
        {
            using var db = new MySqlConnection(Settings.Database.ConnectionString);

            var sql = @"SELECT guildid, emote, roleid FROM autorole_entries";

            AutoRolesEntries = (await db.QueryAsync<AutoRoleEntry>(sql, new { })).ToList();

            return true;
        }
    }

    public class AutoRoleEntry
    {
        public ulong GuildId;
        public string Emote;
        public ulong RoleId;

        public AutoRoleEntry(ulong guildid, string emote, ulong roleid)
        {
            GuildId = guildid;
            Emote = emote;
            RoleId = roleid;
        }
    }
}