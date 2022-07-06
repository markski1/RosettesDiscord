using Dapper;
using MySqlConnector;
using Rosettes.Core;

namespace Rosettes.Modules.Engine
{
    public static class RequestEngine
    {
        private static readonly System.Timers.Timer RequestTimer = new();

        public static void Initialize()
        {
            RequestTimer.Elapsed += RequestHandler;
            RequestTimer.Interval = 10000;
            RequestTimer.AutoReset = true;
            RequestTimer.Enabled = true;
        }

        public static async void RequestHandler(object? source, System.Timers.ElapsedEventArgs e)
        {
            using var db = new MySqlConnection(Settings.Database.ConnectionString);

            var sql = @"SELECT * FROM requests";

            var result = await db.QueryAsync<Request>(sql, new { });

            foreach (Request req in result)
            {
                Guild guild = GuildEngine.GetDBGuildById(req.RelevantGuild);
                switch (req.RequestType)
                {
                    // req type 0: assign role to everyone
                    case 0:
                        if (guild is null) continue;
                        guild.SetRoleForEveryone(req.RelevantValue);
                        break;
                    // req type 1: make guild update
                    case 1:
                        if (guild is null) continue;
                        await GuildEngine.UpdateGuild(guild);
                        break;
                    // req type 2: refresh autoroles
                    case 2:
                        AutoRolesEngine.SyncWithDatabase();
                        break;
                }
                sql = @"DELETE FROM requests WHERE relevantguild=@RelevantGuild AND relevantvalue=@RelevantValue";

                await db.ExecuteAsync(sql, new { req.RelevantGuild, req.RelevantValue });
            }
        }
    }

    public class Request
    {
        public uint RequestType;
        public ulong RelevantGuild;
        public ulong RelevantValue;
        public string RelevantStringValue;

        
        public Request(System.UInt32 requesttype, System.UInt64 relevantguild, System.UInt64 relevantvalue, System.String relevantstringvalue)
        {
            RequestType = requesttype;
            RelevantGuild = relevantguild;
            RelevantValue = relevantvalue;
            RelevantStringValue = relevantstringvalue;
        }
    }
}