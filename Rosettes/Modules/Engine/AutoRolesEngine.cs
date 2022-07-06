using Dapper;
using MySqlConnector;
using Rosettes.Core;

namespace Rosettes.Modules.Engine
{
    public static class AutoRolesEngine
    {
        private static List<AutoRoleEntry> AutoRolesEntries = new();

        public static ulong GetRoleId(ulong guildid, string emoteName)
        {
            IEnumerable<AutoRoleEntry> GuildEntries;
            GuildEntries =
                from role in AutoRolesEntries
                where role.GuildId == guildid
                select role;
            AutoRoleEntry found = GuildEntries.First(item => item.Emote == emoteName);
            if (found is null) return 0;
            return found.RoleId;
        }

        public static async void SyncWithDatabase()
           
        {
            using var db = new MySqlConnection(Settings.Database.ConnectionString);

            var sql = @"SELECT guildid, emote, roleid FROM autorole_entries";

            AutoRolesEntries = (await db.QueryAsync<AutoRoleEntry>(sql, new { })).ToList();
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