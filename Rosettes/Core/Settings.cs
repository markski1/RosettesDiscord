using Discord;
using MySqlConnector;
using Newtonsoft.Json;


namespace Rosettes.Core
{
    public static class Settings
    {
        public static readonly string Token = LoadSetting("token");
        public static readonly string SteamDevKey = LoadSetting("steam");
        public static readonly string FFXIVApiKey = LoadSetting("ffxiv");
        public static readonly string RapidAPIKey = LoadSetting("rapidapi");
        public static readonly dynamic? LavaLinkData = LoadJsonSetting("lavalink");
        public static readonly dynamic? LavaLinkBackup = LoadJsonSetting("lavalinkbackup");
        public static readonly dynamic? LavaLinkBackup2 = LoadJsonSetting("lavalinkbackupbackup");
        public static readonly MySqlConnectionStringBuilder Database = new();

        #if DEBUG
        public static readonly LogSeverity LogSeverity = LogSeverity.Debug;
        #else
        public static readonly LogSeverity LogSeverity = LogSeverity.Info;
        #endif
        
        // not really connecting to the database as much as it's loading the database object with data
        // but it's clearer like this
        public static bool ConnectToDatabase()
        {
            dynamic? MySQLData = LoadJsonSetting("mysql");
            if (MySQLData is null)
            {
                return false;
            }
            Database.Server = MySQLData.Server;
            Database.UserID = MySQLData.UserID;
            Database.Password = MySQLData.Password;
            Database.Database = MySQLData.Database;
            return true;
        }

        // DEBUG means my windows machine, otherwise it's production
        public static string LoadSetting(string name)
        {
            #if DEBUG
            return File.ReadAllText($"Y:/rosetteskeys/{name}.txt").Replace("\n", String.Empty);
            #else
            return File.ReadAllText($"./keys/{name}.txt").Replace("\n", String.Empty);
            #endif
        }

        public static dynamic? LoadJsonSetting(string name)
        {
            #if DEBUG
            return JsonConvert.DeserializeObject(File.ReadAllText($"Y:/rosetteskeys/{name}.txt"));
            #else
            return JsonConvert.DeserializeObject(File.ReadAllText($"./keys/{name}.txt"));
            #endif
        }
    }
}