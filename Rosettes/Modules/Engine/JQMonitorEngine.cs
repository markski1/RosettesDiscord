using Discord;
using Discord.Audio;
using Discord.WebSocket;

namespace Rosettes.Modules.Engine
{
    public static class JQMonitorEngine
    {
        static readonly Dictionary<SocketGuild, JQMonitor> monitorList = new();

        public static async Task<string> StartMonitoring(SocketGuildUser user)
        {
            if (user.VoiceChannel is null) return "You are not in VC.";

            if (monitorList.ContainsKey(user.Guild))
            {
                return "This guild already has an active monitor.";
            }

            var monitor = new JQMonitor(user);

            try
            {
                await monitor.SetupMonitoring();
                monitorList.Add(user.Guild, monitor);
                monitor.StartMonitoring();
                return "VC joins and quits are now monitored and informed in the voice channel's chat.";
            }
            catch
            {
                return "There was an error connecting.";
            }
        }

        public static bool IsMonitored(SocketGuild guild)
        {
            return monitorList.ContainsKey(guild);
        }

        public static string StopMonitoring(SocketGuildUser user)
        {
            if (!monitorList.ContainsKey(user.Guild))
            {
                return "There is no monitoring happening in this guild.";
            }

            monitorList[user.Guild].StopMonitoring();

            monitorList.Remove(user.Guild);

            return "Monitoring stopped.";
        }
    }

    public class JQMonitor
    {
        readonly SocketVoiceChannel voiceChannel;
        //IAudioClient? client;
        List<SocketGuildUser> currentUsers = new();

        bool running = true;

        public JQMonitor(SocketGuildUser user)
        {
            voiceChannel = user.VoiceChannel as SocketVoiceChannel;
        }

        public async Task SetupMonitoring()
        {
            //client = await voiceChannel.ConnectAsync();
            currentUsers = voiceChannel.ConnectedUsers.ToList();
        }

        public async void StartMonitoring()
        {
            int leaveThreshold = 0;
            while (running)
            {
                //if (client is null)
                //{
                //    running = false;
                //    return;
                //}

                //if (client.ConnectionState == ConnectionState.Connected)
                //{
                    List<SocketGuildUser> checkUsers = voiceChannel.ConnectedUsers.ToList();

                    bool changes = false;

                    foreach (SocketGuildUser user in checkUsers)
                    {
                        if (!currentUsers.Contains(user))
                        {
                            changes = true;
                            await voiceChannel.SendMessageAsync($"{user.DisplayName} joined the channel.");
                        }
                    }

                    foreach (SocketGuildUser user in currentUsers)
                    {
                        if (!checkUsers.Contains(user))
                        {
                            changes = true;
                            await voiceChannel.SendMessageAsync($"{user.DisplayName} left the channel.");
                        }
                    }

                    if (changes) currentUsers = checkUsers;

                    if (currentUsers.Count <= 1)
                    {
                        leaveThreshold++;
                        if (leaveThreshold == 10)
                        {
                            StopMonitoring();
                        }
                    }
                    else
                    {
                        leaveThreshold = 0;
                    }
                //}
                //else if (client.ConnectionState == ConnectionState.Disconnected)
                //{
                //    running = false;
                //}

                await Task.Delay(1500);
            }
        }

        public void StopMonitoring()
        {
            running = false;
            //voiceChannel.DisconnectAsync();
        }
    }
}