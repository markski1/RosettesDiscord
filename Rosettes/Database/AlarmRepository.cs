using Dapper;
using Rosettes.Core;
using Rosettes.Modules.Commands.Alarms;

namespace Rosettes.Database;

public class AlarmRepository
{
    public async Task<IEnumerable<Alarm>> GetAllAlarmsAsync()
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"SELECT datetime, user, channel FROM alarms";

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

    public async Task<bool> InsertAlarm(Alarm alarm)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"INSERT INTO alarms (datetime, user, channel)
                        VALUES(@DateTime, @User, @Channel)";

        if (alarm.Channel is null) return false;

        try
        {
            return (await db.ExecuteAsync(sql, new { alarm.DateTime, User = alarm.User.Id, Channel = alarm.Channel.Id })) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-insertalarm", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteAlarm(Alarm alarm)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"DELETE FROM alarms
                        WHERE user = @User";
        try
        {
            return (await db.ExecuteAsync(sql, new { User = alarm.User.Id })) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-deletealarm", $"sqlException code {ex.Message}");
            return false;
        }
    }
}