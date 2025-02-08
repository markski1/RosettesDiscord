using Discord;
using Discord.WebSocket;
using Rosettes.Core;

namespace Rosettes.Modules.Engine.Guild;

public static class JqMonitorEngine
{
    public static Task UserVcUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
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
                var dbGuild = await GuildEngine.GetDbGuild(channel.Guild);
                if (!dbGuild.MonitorsVc()) return;
                ChannelInform(user, channel, action);
                if (channel2 is not null && action2 is not null)
                    ChannelInform(user, channel, action);
            }
        });

        return Task.CompletedTask;
    }

    private static async void ChannelInform(SocketUser user, SocketVoiceChannel channel, string action)
    {
        try
        {
            IMessage message = await channel.SendMessageAsync($"{user.Username} {action} the channel.");
            _ = new MessageDeleter(message, 30);
        }
        catch
        {
            // nothing to handle, means we have no access, just don't crash.
        }
    }
}