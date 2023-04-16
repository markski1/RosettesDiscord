using Discord;
using Discord.WebSocket;
using Rosettes.Core;

namespace Rosettes.Modules.Engine.Guild
{
    public static class JQMonitorEngine
    {
        public static Task UserVCUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            _ = Task.Run(async () =>
            {
                SocketVoiceChannel? channel = null; SocketVoiceChannel? channel2 = null;
                string? action = null; string? action2 = null;
                if (oldState.VoiceChannel is not null && newState.VoiceChannel is not null)
                {
                    if (oldState.VoiceChannel != newState.VoiceChannel)
                    {
                        channel = oldState.VoiceChannel;
                        channel2 = newState.VoiceChannel;
                        action = "left";
                        action2 = "joined";
                    }
                }
                else if (oldState.VoiceChannel is not null)
                {
                    channel = oldState.VoiceChannel;
                    action = "left";
                }
                else if (newState.VoiceChannel is not null)
                {
                    channel = newState.VoiceChannel;
                    action = "joined";
                }

                if (channel is not null && action is not null)
                {
                    var dbGuild = await GuildEngine.GetDBGuild(channel.Guild);
                    if (!dbGuild.MonitorsVC()) return;
                    ChannelInform(user, channel, action);
                    if (channel2 is not null && action2 is not null)
                        ChannelInform(user, channel, action);
                }
            });

            return Task.CompletedTask;
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
            try
            {
                IMessage message = await channel.SendMessageAsync($"{name} {action} the channel.");
                _ = new MessageDeleter(message, 60);
            }
            catch
            {
                // nothing to handle, means we have no access, just don't crash.
            }
        }
    }
}