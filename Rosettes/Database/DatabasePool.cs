using MySqlConnector;
using Settings = Rosettes.Core.Settings;

namespace Rosettes.Database;

public static class DatabasePool
{
    public static MySqlConnection GetConnection() => new(Settings.Database.ConnectionString);
}
