using Discord.WebSocket;
using System.Text.RegularExpressions;

namespace Rosettes.Modules.Engine
{
    public static class JQMonitorEngine
    {
        public static async Task UserVCUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            SocketVoiceChannel? channel = null;
            string? action = null;
            if (oldState.VoiceChannel is not null)
            {
                channel = oldState.VoiceChannel;

                if (newState.VoiceChannel is null)
                    action = "left";
            }
            else if (newState.VoiceChannel is not null)
            {
                channel = newState.VoiceChannel;

                if (oldState.VoiceChannel is null)
                    action = "joined";
            }

            if (channel is not null && action is not null)
            {
                var dbGuild = await GuildEngine.GetDBGuild(channel.Guild);
                if (!dbGuild.MonitorsVC()) return;
                ChannelInform(user, channel, action);
            }
        }

        public static async void ChannelInform(SocketUser user, SocketVoiceChannel channel, string action)
        {
            string name;
            if (user is SocketGuildUser guildUser)
            {
                name = guildUser.DisplayName.Replace(" ", "");
            }
            else
            {
                name = user.Username.Replace(" ", "");
            }
            await channel.SendMessageAsync($"{name} {action} the channel.");
        }
    }
}