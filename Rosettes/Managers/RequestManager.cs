using Dapper;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Managers
{
    public static class RequestManager
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

            var sql = @"SELECT requesttype, relevantguild, relevantvalue, relevantstringvalue FROM requests";

            var result = await db.QueryAsync<Request>(sql, new { });

            foreach (Request req in result)
            {
                Guild? guild = null;
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
                    // req type 2: refresh autoroles
                    case 2:
                        await AutoRolesEngine.SyncWithDatabase();
                        break;
                    // req type 3: message to given guild or user.
                    case 3:
                        Global.SendMessage(req.RelevantGuild, req.RelevantStringValue);
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


        public Request(uint requesttype, ulong relevantguild, ulong relevantvalue, string relevantstringvalue)
        {
            RequestType = requesttype;
            RelevantGuild = relevantguild;
            RelevantValue = relevantvalue;
            RelevantStringValue = relevantstringvalue;
        }
    }
}