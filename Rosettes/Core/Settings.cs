using Discord;
using MySqlConnector;
using Newtonsoft.Json;

namespace Rosettes.Core;

public static class Settings
{
    // DEBUG is my local machine
#if DEBUG
    public static readonly LogSeverity LogSeverity = LogSeverity.Debug;
    public static readonly string keyLoc = "/home/markski/rosetteskeys";
    // otherwise, it's production.
#else
    public static readonly LogSeverity LogSeverity = LogSeverity.Info;
    public static readonly string keyLoc = "./keys";
#endif
    
    public static readonly string Token = LoadSetting("token");
    public static readonly string SteamDevKey = LoadSetting("steam");
    public static readonly string FFXIVApiKey = LoadSetting("ffxiv");
    public static readonly string RapidAPIKey = LoadSetting("rapidapi");
    public static readonly string SauceNAO = LoadSetting("saucenao");
    public static readonly string SecretKey = LoadSetting("secretkey");
    public static readonly MySqlConnectionStringBuilder Database = new();


    public static bool LoadDatabaseObj()
    {
        dynamic? mySqlData = LoadJsonSetting("mysql");
        if (mySqlData is null)
        {
            return false;
        }
        Database.Server = mySqlData.Server;
        Database.UserID = mySqlData.UserID;
        Database.Password = mySqlData.Password;
        Database.Database = mySqlData.Database;
        return true;
    }

    private static string LoadSetting(string name) => File.ReadAllText($"{keyLoc}/{name}.txt").Replace("\n", String.Empty);

    // Supressing this warning, because if this were to be null, crashing on launch IS the desired effect.
    // Rosettes cannot and should not work with misconfused keys.
#pragma warning disable CS8603
    private static dynamic LoadJsonSetting(string name) => JsonConvert.DeserializeObject(File.ReadAllText($"{keyLoc}/{name}.txt"));
#pragma warning restore CS8603
}