﻿using Discord;
using MySqlConnector;
using Newtonsoft.Json;

namespace Rosettes.Core;

public static class Settings
{
#if DEBUG
    // DEBUG is my local machine
    public static readonly LogSeverity LogSeverity = LogSeverity.Debug;
    private const string KeyLoc = "/home/markski/rosetteskeys";
#else
    // otherwise, it's production.
    public static readonly LogSeverity LogSeverity = LogSeverity.Info;
    private const string KeyLoc = "./keys";
#endif
    
    public static readonly string Token = LoadSetting("token");
    public static readonly string SteamDevKey = LoadSetting("steam");
    public static readonly string FfxivApiKey = LoadSetting("ffxiv");
    public static readonly string RapidApiKey = LoadSetting("rapidapi");
    public static readonly string SauceNao = LoadSetting("saucenao");
    public static readonly string SecretKey = LoadSetting("secretkey");
    public static readonly string OpenAi = LoadSetting("openai");
    public static readonly string OpenAiPrompt = LoadSetting("openai_prompt");
    public static readonly MySqlConnectionStringBuilder Database = new();


    public static bool LoadDatabaseObj()
    {
        dynamic mySqlData = LoadJsonSetting("mysql");

        Database.Server = mySqlData.Server;
        Database.UserID = mySqlData.UserID;
        Database.Password = mySqlData.Password;
        Database.Database = mySqlData.Database;
        return true;
    }

    private static string LoadSetting(string name)
    {
        try
        {
            return File.ReadAllText($"{KeyLoc}/{name}.txt").Replace("\n", string.Empty);
        }
        catch
        {
            Global.GenerateErrorMessage("settings", $"Key not loaded: {name}");
            return "";
        }
    }

    // Supressing this warning, because if this were to be null, crashing on launch IS the desired effect.
    // Rosettes cannot and should not work with misconfused keys.
#pragma warning disable CS8603
    private static dynamic LoadJsonSetting(string name) => JsonConvert.DeserializeObject(File.ReadAllText($"{KeyLoc}/{name}.txt"));
#pragma warning restore CS8603
}