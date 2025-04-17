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

    public static async Task LoadAllAlarmsFromDatabase()
    {
        IEnumerable<Alarm> activeAlarms = await AlarmRepository.GetAllAlarmsAsync();
        _activeAlarms = activeAlarms.ToList();
    }

    public static async Task<bool> CreateAlarm(DateTime dateTime, User user, ISocketMessageChannel channel, int minutes)
    {
        try
        {
            Alarm newAlarm = new(dateTime, user, channel, minutes);
            await AlarmRepository.InsertAlarm(newAlarm);
            _activeAlarms.Add(newAlarm);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task DeleteAlarm(Alarm alarm)
    {
        alarm.Timer.Stop();
        await AlarmRepository.DeleteAlarm(alarm);
        _activeAlarms.Remove(alarm);
    }

    public static bool CheckUserHasAlarm(IUser user)
    {
        Alarm? findAlarm =  _activeAlarms.Find(item => item.User.Id == user.Id);
        return findAlarm != null;
    }

    public static Alarm? GetUserAlarm(IUser user)
    {
        Alarm? findAlarm = _activeAlarms.Find(item => item.User.Id == user.Id);
        return findAlarm;
    }
}

public class Alarm
{
    public readonly DateTime DateTime;
    public readonly User User;
    public readonly System.Timers.Timer Timer;
    public ISocketMessageChannel? Channel;

    private readonly bool _success;

    // constructor used by /alarm
    public Alarm(DateTime dateTime, User user, ISocketMessageChannel channel, int minutes)
    {
        DateTime = dateTime;
        User = user;

        Timer = new(minutes * 60 * 1000);
        Timer.Elapsed += AlarmRing;
        Timer.AutoReset = false;
        Timer.Enabled = true;
        Channel = channel;

        _success = true;
    }

    // constructor used when loading from database
    public Alarm(DateTime dateTime, ulong user, ulong channel)
    {
        DateTime = dateTime;
        User = UserEngine.GetCachedDbUserById(user);

        double amount;
        // if we are still in time, just restart the alarm as intended.
        if (dateTime > DateTime.Now)
        {
            amount = (dateTime - DateTime.Now).TotalSeconds;
            _success = true;
        }
        // otherwise, wait 5 seconds and let the user know we failed.
        else
        {
            amount = 5;
            _success = false;
        }

        Timer = new(amount * 1000);
        Timer.Elapsed += AlarmRing;
        Timer.AutoReset = false;
        Timer.Enabled = true;
        using DiscordSocketClient client = ServiceManager.GetService<DiscordSocketClient>();
        Channel = client.GetChannel(channel) as ISocketMessageChannel;
    }

    public async void AlarmRing(object? source, System.Timers.ElapsedEventArgs e)
    {
        // first remove the alarm off the database.
        await AlarmManager.DeleteAlarm(this);
        // if the database constructior failed to load the channel.
        IUser? discordRef = await User.GetDiscordReference();
        if (discordRef is null)
        {
            Global.GenerateErrorMessage("alarm", $"Sadly, I have failed to deliver an alarm to {await User.GetName()}. Error 1");
            return;
        }
        if (Channel is null)
        {
            // we establish a channel through DM.
            Channel = await discordRef.CreateDMChannelAsync() as ISocketMessageChannel;
            // If we can't establish a channel, Rosettes has failed to alert the user.
            if (Channel is null)
            {
                Global.GenerateErrorMessage("alarm", $"Sadly, I have failed to deliver an alarm to {await User.GetName()}. Error 2");
                return;
            }
        }

        EmbedBuilder embed = await Global.MakeRosettesEmbed(User);

        embed.Title = "Ring!";
        embed.Description = $"Alarm for {discordRef.Mention}!";

        if (_success)
        {
            await Channel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            var findUser = await User.GetDiscordReference();
            if (findUser is not null)
            {
                await Channel.SendMessageAsync($"I'm sorry, {findUser.Mention} - It seems like I was shut down during the time I was meant to deliver your alarm... ({DateTime:ddd, dd MMM yyy; HH: mm: ss})");   
            }
        }
    }
}