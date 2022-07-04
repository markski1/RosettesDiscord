using Dapper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MySqlConnector;
using Newtonsoft.Json;
using Rosettes.Core;

namespace Rosettes.Modules.Engine
{
    public static class RequestEngine
    {
        private static readonly System.Timers.Timer RequestTimer = new();

        public static void Initialize()
        {
            RequestTimer.Elapsed += RequestHandler;
            RequestTimer.Interval = 30000;
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
                switch (req.RequestType)
                {
                    case 0:
                        Guild guild = GuildEngine.GetDBGuildById(req.RelevantGuild);
                        if (guild is null) continue;
                        guild.SetRoleForEveryone(req.RelevantValue);
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