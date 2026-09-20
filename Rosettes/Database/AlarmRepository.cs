using Dapper;
using Rosettes.Core;
using Rosettes.Modules.Commands.Alarms;

namespace Rosettes.Database;

public static class AlarmRepository
{
    public static async Task<IEnumerable<Alarm>> GetAllAlarmsAsync()
    {
        using var db = DatabasePool.GetConnection();

        const string sql = "SELECT id, datetime, user, channel, message FROM alarms";

        try
        {
            return await db.QueryAsync<Alarm>(sql, new { });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-getallalarms", $"sqlException code {ex.Message}");
            return new List<Alarm>();
        }
    }

    public static async Task<int?> InsertAlarm(Alarm alarm)
    {
        using var db = DatabasePool.GetConnection();

        const string sql = """
                           INSERT INTO alarms (datetime, user, channel, message)
                           VALUES(@DateTime, @User, @Channel, @Message);
                           SELECT LAST_INSERT_ID();
                           """;

        if (alarm.Channel is null) return null;

        try
        {
            return await db.ExecuteScalarAsync<int>(sql, new
            {
                alarm.DateTime,
                User = alarm.User.Id,
                Channel = alarm.Channel.Id,
                alarm.Message
            });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-insertalarm", $"sqlException code {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> DeleteAlarm(Alarm alarm)
    {
        using var db = DatabasePool.GetConnection();

        const string sql = """
                           DELETE FROM alarms
                           WHERE id = @Id
                           """;
        try
        {
            return await db.ExecuteAsync(sql, new { Id = alarm.Id }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-deletealarm", $"sqlException code {ex.Message}");
            return false;
        }
    }
}
