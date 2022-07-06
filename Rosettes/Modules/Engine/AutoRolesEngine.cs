using Dapper;
using MySqlConnector;
using Rosettes.Core;

namespace Rosettes.Modules.Engine
{
    public static class AutoRolesEngine
    {
        private static readonly List<AutoRoleEntry> AutoRolesEntries = new();

        public static ulong GetRoleId(ulong guildid, string emoteName)
        {
            IEnumerable<AutoRoleEntry> GuildEntries;
            GuildEntries =
                from role in AutoRolesEntries
                where role.GuildId == guildid
                select role;
            //AutoRoleEntry found = GuildEntries.First(item => item.EmoteId == emoteName);
            //if (found is null) return 0;
            //return found.RoleId;

            return 0;
        }

        public static async void SyncWithDatabase()
           
        {
            using var db = new MySqlConnection(Settings.Database.ConnectionString);

            var sql = @"SELECT * FROM requests";

            var result = await db.QueryAsync<Request>(sql, new { });

            foreach (Request req in result)
            {
                Guild guild;
                switch (req.RequestType)
                {
                    // req type 0: assign role to everyone
                    case 0:
                        guild = GuildEngine.GetDBGuildById(req.RelevantGuild);
                        if (guild is null) continue;
                        guild.SetRoleForEveryone(req.RelevantValue);
                        break;
                    // req type 1: make guild update
                    case 1:
                        guild = GuildEngine.GetDBGuildById(req.RelevantGuild);
                        if (guild is null) continue;
                        await GuildEngine.UpdateGuild(guild);
                        break;
                }
                sql = @"DELETE FROM requests WHERE relevantguild=@RelevantGuild AND relevantvalue=@RelevantValue";

                await db.ExecuteAsync(sql, new { req.RelevantGuild, req.RelevantValue });
            }
        }
    }

    public class AutoRoleEntry
    {
        public ulong GuildId;
        public ulong EmoteId;
        public ulong RoleId;

        public AutoRoleEntry(ulong guildid, ulong emoteid, ulong roleid)
        {
            GuildId = guildid;
            EmoteId = emoteid;
            RoleId = roleid;
        }
    }
}