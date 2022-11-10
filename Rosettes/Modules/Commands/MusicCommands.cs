using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
    [Summary("Commands to control the bot's music playback.")]
    public class MusicCommands : ModuleBase<SocketCommandContext>
    {
        [Command("play")]
        [Summary("Joins your VC, and begins playing the specified song (either a URL or search term). If a song is already playing, it'll be queued.")]
        public async Task PlayMusic([Remainder] string search)
        {
            if (await CheckMusicConditions(Context) == false) return;
            if (Context.User is not SocketGuildUser _socketUser || Context.User is not IVoiceState _voiceState || Context.Channel is not ITextChannel _textChannel)
            {
                await Context.Channel.SendMessageAsync("Something went wrong.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.PlayAsync(_socketUser, Context.Guild, _voiceState, _textChannel, search)
                );
        }

        [Command("stop")]
        [Summary("Stops playing music.")]
        public async Task StopMusic()
        {
            if (await CheckMusicConditions(Context) == false) return;
            await ReplyAsync(
                    await MusicEngine.StopAsync(Context.Guild)
                );
        }

        [Command("skip")]
        [Summary("Skip to the next song in the queue.")]
        public async Task SkipMusic()
        {
            if (await CheckMusicConditions(Context) == false) return;
            await ReplyAsync(
                    await MusicEngine.SkipTrackAsync(Context.Guild)
                );
        }

        [Command("toggle")]
        [Summary("Pauses and resumes the currently playing song.")]
        public async Task ToggleMusic()
        {
            if (await CheckMusicConditions(Context) == false) return;
            await ReplyAsync(
                    await MusicEngine.ToggleAsync(Context.Guild)
                );
        }

        [Command("leave")]
        [Summary("Make the bot leave VC.")]
        public async Task LeaveMusic()
        {
            if (await CheckMusicConditions(Context) == false) return;
            await ReplyAsync(
                    await MusicEngine.LeaveAsync(Context.Guild)
                );
        }


        private static async Task<bool> CheckMusicConditions(SocketCommandContext Context)
        {
            if (Context.Guild == null)
            {
                await Context.Channel.SendMessageAsync("This command won't run in my DM's, silly.");
                return false;
            }
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (!dbGuild.AllowsMusic())
            {
                await Context.Channel.SendMessageAsync("Sorry, but the guild admins have disabled the use of music commands.");
                return false;
            }
            return true;
        }
    }
}
