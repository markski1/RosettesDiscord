using Discord;
using MySqlConnector;

namespace Rosettes.Core;

public static class Settings
{
    public static readonly LogSeverity LogSeverity =
#if DEBUG
        LogSeverity.Debug;
#else
        LogSeverity.Info;
#endif

    private static readonly Dictionary<string, string> EnvVars = LoadEnvFile();

    public static readonly string Token = GetEnv("TOKEN");
    public static readonly string SteamDevKey = GetEnv("STEAM_DEV_KEY");
    public static readonly string FfxivApiKey = GetEnv("FFXIV_API_KEY");
    public static readonly string RapidApiKey = GetEnv("RAPIDAPI_KEY");
    public static readonly string SauceNao = GetEnv("SAUCENAO_KEY");
    public static readonly string SecretKey = GetEnv("SECRET_KEY");
    public static readonly string ApiKey = GetEnv("LLM_KEY");
    public static readonly string SystemPrompt = LoadTextFile("system_prompt.txt");
    public static readonly MySqlConnectionStringBuilder Database = new();

    public static bool LoadDatabaseObj()
    {
        Database.Server = GetEnv("MYSQL_SERVER");
        Database.UserID = GetEnv("MYSQL_USERID");
        Database.Password = GetEnv("MYSQL_PASSWORD");
        Database.Database = GetEnv("MYSQL_DATABASE");
        Database.Pooling = true;
        Database.MinimumPoolSize = 2;
        Database.MaximumPoolSize = 15;
        Database.ConnectionIdleTimeout = 300;
        return true;
    }

    private static string GetEnv(string key)
    {
        if (EnvVars.TryGetValue(key, out var value))
            return value;

        Global.GenerateErrorMessage("settings", $"Key not found in .env: {key}");
        return "";
    }

    private static string LoadTextFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            return File.ReadAllText(path).Replace("\n", string.Empty);
        }
        catch
        {
            Global.GenerateErrorMessage("settings", $"File not loaded: {fileName}");
            return "";
        }
    }

    private static Dictionary<string, string> LoadEnvFile()
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var envPath = Path.Combine(AppContext.BaseDirectory, ".env");

        if (!File.Exists(envPath))
        {
            Global.GenerateErrorMessage("settings", ".env file not found next to the binary.");
            return vars;
        }

        foreach (var rawLine in File.ReadLines(envPath))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            vars[key] = value;
        }

        return vars;
    }
}