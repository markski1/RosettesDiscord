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
        [Summary("Joins your VC, and begins playing the specified song. If a song is already playing, it'll be queued.")]
        public async Task PlayMusic([Remainder] string search)
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (!dbGuild.AllowsMusic())
            {
                await ReplyAsync("Sorry, but the guild admins have disabled the use of this type of commands.");
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
        public async Task StopMusic()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (!dbGuild.AllowsMusic())
            {
                await ReplyAsync("Sorry, but the guild admins have disabled the use of this type of commands.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.StopAsync(Context.Guild)
                );
        }

        [Command("skip")]
        [Summary("Skip to the next song in the queue.")]
        public async Task SkipMusic()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (!dbGuild.AllowsMusic())
            {
                await ReplyAsync("Sorry, but the guild admins have disabled the use of this type of commands.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.SkipTrackAsync(Context.Guild)
                );
        }

        [Command("toggle")]
        [Summary("Pauses and resumes the currently playing song.")]
        public async Task ToggleMusic()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (!dbGuild.AllowsMusic())
            {
                await ReplyAsync("Sorry, but the guild admins have disabled the use of this type of commands.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.ToggleAsync(Context.Guild)
                );
        }

        [Command("leave")]
        [Summary("Make the bot leave VC.")]
        public async Task LeaveMusic()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync("This command won't run in my DM's, silly.");
                return;
            }
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (!dbGuild.AllowsMusic())
            {
                await ReplyAsync("Sorry, but the guild admins have disabled the use of this type of commands.");
                return;
            }
            await ReplyAsync(
                    await MusicEngine.LeaveAsync(Context.Guild)
                );
        }

    }
}
