using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Managers;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands.Alarms;

public static class AlarmManager
{
    private static List<Alarm> _activeAlarms = [];
    private static readonly Lock AlarmsLock = new();

    public static async Task LoadAllAlarmsFromDatabase()
    {
        IEnumerable<Alarm> activeAlarms = await AlarmRepository.GetAllAlarmsAsync();
        var loaded = activeAlarms.ToList();
        lock (AlarmsLock)
        {
            _activeAlarms = loaded;
        }
        foreach (var alarm in loaded) alarm.Start();
    }

    public static async Task<bool> CreateAlarm(DateTime dateTime, User user, ISocketMessageChannel channel, string message)
    {
        try
        {
            Alarm newAlarm = new(dateTime, user, channel, message);
            int? alarmId = await AlarmRepository.InsertAlarm(newAlarm);
            if (alarmId is null)
            {
                newAlarm.Timer.Dispose();
                return false;
            }

            newAlarm.Id = alarmId.Value;
            lock (AlarmsLock)
            {
                _activeAlarms.Add(newAlarm);
            }
            newAlarm.Start();

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> DeleteAlarm(Alarm alarm)
    {
        if (!await AlarmRepository.DeleteAlarm(alarm)) return false;
        alarm.Timer.Stop();
        lock (AlarmsLock)
        {
            _activeAlarms.Remove(alarm);
        }
        alarm.Timer.Dispose();
        return true;
    }

    public static List<Alarm> GetUserAlarms(IUser user)
    {
        lock (AlarmsLock)
        {
            return _activeAlarms.Where(item => item.User.Id == user.Id).ToList();
        }
    }
}

public class Alarm
{
    private const double MaxTimerIntervalMilliseconds = 24 * 60 * 60 * 1000;
    private const double RetryIntervalMilliseconds = 5 * 60 * 1000;
    public int Id;
    public string Message;
    public readonly DateTime DateTime;
    public readonly User User;
    public readonly System.Timers.Timer Timer;
    public ISocketMessageChannel? Channel;

    private readonly bool _missedWhileOffline;
    private bool _delivered;

    // constructor used by /reminder
    public Alarm(DateTime dateTime, User user, ISocketMessageChannel channel, string message)
    {
        DateTime = dateTime;
        User = user;

        Timer = new();
        Timer.Elapsed += AlarmRing;
        Timer.AutoReset = false;
        Channel = channel;
        Id = 0;
        Message = message;

        _missedWhileOffline = false;
    }

    // constructor used when loading from database
    public Alarm(uint id, DateTime dateTime, ulong user, ulong channel, string message)
    {
        DateTime = dateTime;
        User = UserEngine.GetCachedDbUserById(user);
        Id = (int)id;
        Message = message;

        _missedWhileOffline = dateTime <= DateTime.Now;
        Timer = new();
        Timer.Elapsed += AlarmRing;
        Timer.AutoReset = false;
        var client = ServiceManager.GetService<DiscordSocketClient>();
        Channel = client.GetChannel(channel) as ISocketMessageChannel;
    }

    public void Start()
    {
        Timer.Interval = Math.Clamp((DateTime - DateTime.Now).TotalMilliseconds, 1, MaxTimerIntervalMilliseconds);
        Timer.Start();
    }

    private void AlarmRing(object? source, System.Timers.ElapsedEventArgs e) => _ = DeliverAsync();

    private async Task DeliverAsync()
    {
        try
        {
            if (DateTime > DateTime.Now)
            {
                Start();
                return;
            }

            if (!_delivered)
            {
                IUser? discordRef = await User.GetDiscordReference();
                if (discordRef is null)
                    throw new InvalidOperationException($"Could not find reminder user {User.Id}.");

                if (Channel is null)
                {
                    Channel = await discordRef.CreateDMChannelAsync() as ISocketMessageChannel;
                    if (Channel is null)
                        throw new InvalidOperationException($"Could not open a channel for reminder {Id}.");
                }

                if (!_missedWhileOffline)
                {
                    EmbedBuilder embed = await Global.MakeRosettesEmbed(User);
                    embed.Title = "Hey!";
                    embed.Description = $"Reminder for {discordRef.Mention}.";
                    if (Message.Length > 0) embed.AddField("Message", Message);
                    await Channel.SendMessageAsync(discordRef.Mention, embed: embed.Build());
                }
                else
                {
                    await Channel.SendMessageAsync($"I'm sorry, {discordRef.Mention} - It seems like I was shut down during the time I was meant to deliver your reminder... ({DateTime:ddd, dd MMM yyyy; HH:mm:ss})");
                }

                _delivered = true;
            }

            if (!await AlarmManager.DeleteAlarm(this))
                throw new InvalidOperationException($"Could not remove delivered reminder {Id} from the database.");
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("reminder", $"Failed to deliver reminder {Id}: {ex}");
            Timer.Interval = RetryIntervalMilliseconds;
            Timer.Start();
        }
    }
}
