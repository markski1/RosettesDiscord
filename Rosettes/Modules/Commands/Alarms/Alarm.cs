using Rosettes.Modules.Engine;
using Rosettes.Database;
using Discord.WebSocket;
using Rosettes.Core;
using Discord;

namespace Rosettes.Modules.Commands.Alarms
{
    public static class AlarmManager
    {
        private static List<Alarm> ActiveAlarms = new();
        public static readonly AlarmRepository _interface = new();

        public static async void LoadAllAlarmsFromDatabase()
        {
            IEnumerable<Alarm> activeAlarms;
            activeAlarms = await _interface.GetAllAlarmsAsync();
            ActiveAlarms = activeAlarms.ToList();
        }

        public static async void CreateAlarm(DateTime dateTime, User user, ISocketMessageChannel channel, int seconds)
        {
            Alarm newAlarm = new(dateTime, user, channel, seconds);
            await _interface.InsertAlarm(newAlarm);
            ActiveAlarms.Add(newAlarm);
        }

        public static async void DeleteAlarm(Alarm alarm)
        {
            alarm.Timer.Stop();
            await _interface.DeleteAlarm(alarm);
            ActiveAlarms.Remove(alarm);
        }

        public static bool CheckUserHasAlarm(IUser user)
        {
            Alarm? findAlarm = null;
            findAlarm = ActiveAlarms.Find(item => item.User.Id == user.Id);
            return findAlarm != null;
        }

        public static Alarm? GetUserAlarm(IUser user)
        {
            Alarm? findAlarm = null;
            findAlarm = ActiveAlarms.Find(item => item.User.Id == user.Id);
            return findAlarm;
        }
    }

    public class Alarm
    {
        public readonly DateTime DateTime;
        public readonly User User;
        public readonly System.Timers.Timer Timer;
        public ISocketMessageChannel? Channel;

        private readonly bool Success;

        // constructor used by $alarm
        public Alarm(DateTime dateTime, User user, ISocketMessageChannel channel, int seconds)
        {
            DateTime = dateTime;
            User = user;

            Timer = new(seconds * 1000);
            Timer.Elapsed += AlarmRing;
            Timer.AutoReset = false;
            Timer.Enabled = true;
            Channel = channel;

            Success = true;
        }

        // constructor used when loading from database
        public Alarm(DateTime dateTime, ulong user, ulong channel)
        {
            DateTime = dateTime;
            User = UserEngine.GetDBUserById(user);

            double amount;
            // if we are still in time, just restart the alarm as intended.
            if (dateTime > DateTime.Now)
            {
                amount = (dateTime - DateTime.Now).TotalSeconds;
                Success = true;
            }
            // otherwise, wait 5 seconds and let the user know we failed.
            else
            {
                amount = 5;
                Success = false;
            }

            Timer = new(amount * 1000);
            Timer.Elapsed += AlarmRing;
            Timer.AutoReset = false;
            Timer.Enabled = true;
            DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
            Channel = _client.GetChannel(channel) as ISocketMessageChannel;
        }

        public async void AlarmRing(Object? source, System.Timers.ElapsedEventArgs e)
        {
            // first remove the alarm off the database.
            AlarmManager.DeleteAlarm(this);
            // if the database constructior failed to load the channel.
            if (Channel is null)
            {
                // we establish a channel through DM.
                Channel = (await User.GetDiscordReference()).CreateDMChannelAsync() as ISocketMessageChannel;
                // If we can't establish a channel, Rosettes has failed to alert the user.
                if (Channel is null)
                {
                    Global.GenerateErrorMessage("alarm", $"Sadly, I have failed to deliver an alarm to {await User.GetName()}");
                    return;
                }
            }
            if (Success)
            {
                await Channel.SendMessageAsync($"Ring! {(await User.GetDiscordReference()).Mention} !");
            }
            else
            {
                await Channel.SendMessageAsync($"I'm sorry, {(await User.GetDiscordReference()).Mention} - It seems like I was shut down during the time I was meant to deliver your alarm... ({DateTime:ddd, dd MMM yyy; HH: mm: ss})");
            }
        }
    }
}