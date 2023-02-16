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
            if (newState.VoiceChannel is not null)
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

            return;
        }

        public static async void ChannelInform(SocketUser user, SocketVoiceChannel channel, string action)
        {
            string cleanName = user.Username.Replace(" ", "");
            Regex rgx = new("[^a-zA-Z0-9 -]");
            cleanName = rgx.Replace(cleanName, "");
            cleanName += $"#{user.Discriminator}";
            await channel.SendMessageAsync($"{cleanName} has {action} the channel.");
        }
    }
}