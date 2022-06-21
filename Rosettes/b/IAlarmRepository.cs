using Discord;
using Rosettes.Modules.Commands.Alarms;

namespace Rosettes.Database
{
    public interface IAlarmRepository
    {
        Task<IEnumerable<Alarm>> GetAllAlarmsAsync();
        Task<bool> InsertAlarm(Alarm alarm);
        Task<bool> DeleteAlarm(Alarm alarm);
    }
}
