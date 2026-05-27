using MySqlConnector;
using Settings = Rosettes.Core.Settings;

namespace Rosettes.Database;

public static class DatabasePool
{
    public static DbConnectionWrapper GetConnection()
    {
        var newConn = new MySqlConnection(Settings.Database.ConnectionString);
        return new DbConnectionWrapper(newConn);
    }
}

public class DbConnectionWrapper(MySqlConnection connection) : IDisposable
{
    public readonly MySqlConnection Db = connection;

    public void Dispose()
    {
        Db.Dispose();
        GC.SuppressFinalize(this);
    }
}