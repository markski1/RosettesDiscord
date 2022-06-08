using Discord;
using MySqlConnector;
using Newtonsoft.Json;

#pragma warning disable CS8602
#pragma warning disable CS8601
namespace Rosettes.core
{
    public static class Settings
    {
        public static readonly char Prefix = '$';
#if DEBUG
        public static readonly string Token = File.ReadAllText("Y:/token.txt");
        public static readonly string SteamDevKey = File.ReadAllText("Y:/steam.txt");
        public static readonly string FFXIVApiKey = File.ReadAllText("Y:/ffxiv.txt");
        public static readonly dynamic LavaLinkData = JsonConvert.DeserializeObject(File.ReadAllText("Y:/lavalink.txt"));
        public static readonly dynamic MySQLData = JsonConvert.DeserializeObject(File.ReadAllText("Y:/mysql.txt"));
        public static readonly LogSeverity LogSeverity = LogSeverity.Debug;
#else
        public static readonly string Token = File.ReadAllText("./keys/token.txt");
        public static readonly string SteamDevKey = File.ReadAllText("./keys/steam.txt");
        public static readonly string FFXIVApiKey = File.ReadAllText("./keys/ffxiv.txt");
        public static readonly dynamic LavaLinkData = JsonConvert.DeserializeObject(File.ReadAllText("./keys/lavalink.txt"));
        public static readonly dynamic MySQLData = JsonConvert.DeserializeObject(File.ReadAllText("./keys/mysql.txt"));
        public static readonly LogSeverity LogSeverity = LogSeverity.Info;
#endif
        public static readonly MySqlConnectionStringBuilder Database = new() { Server = MySQLData.Server, UserID = MySQLData.UserID, Password = MySQLData.Password, Database = MySQLData.Database };

    }
}
#pragma warning restore CS8601
#pragma warning restore CS8602