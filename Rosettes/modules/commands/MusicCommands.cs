using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Rosettes.modules.engine;

namespace Rosettes.modules.commands
{
    [Summary("Commands to control the bot's music playback.")]
    public class MusicCommands : ModuleBase<SocketCommandContext>
    {
        [Command("play")]
        [Summary("Joins your VC, and begins playing the specified song. If a song is already playing, it'll be queued.")]
        public async Task PlayCommand([Remainder] string search)
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            if (Context.User is not SocketGuildUser _socketUser || Context.User is not IVoiceState _voiceState || Context.Channel is not ITextChannel _textChannel)
            {
                await ReplyAsync("Something went wrong.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.PlayAsync(_socketUser, Context.Guild, _voiceState, _textChannel, search)
                );
        }

        [Command("stop")]
        [Summary("Stops playing music.")]
        public async Task StopCommand()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.StopAsync(Context.Guild)
                );
        }

        [Command("skip")]
        [Summary("Skip to the next song in the queue.")]
        public async Task SkipCommand()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.SkipTrackAsync(Context.Guild)
                );
        }

        [Command("toggle")]
        [Summary("Pauses and resumes the currently playing song.")]
        public async Task PauseCommand()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.ToggleAsync(Context.Guild)
                );
        }

        [Command("leave")]
        [Summary("Make the bot leave VC.")]
        public async Task LeaveCommand()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.LeaveAsync(Context.Guild)
                );
        }

    }
}
