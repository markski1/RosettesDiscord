using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
    public class MusicCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("play", "Joins VC and plays the specified song. Can be a URL or a search term.")]
        public async Task PlayMusic(string URLorSearch)
        {
            if (await CheckMusicConditions(Context) == false) return;
            if (Context.User is not SocketGuildUser _socketUser || Context.User is not IVoiceState _voiceState || Context.Channel is not ITextChannel _textChannel)
            {
                await Context.Channel.SendMessageAsync("Something went wrong.");
                return;
            }
            await RespondAsync(
                    await MusicEngine.PlayAsync(_socketUser, Context.Guild, _voiceState, _textChannel, URLorSearch)
                );
        }

        [SlashCommand("stop", "Stops playing music.")]
        public async Task StopMusic()
        {
            if (await CheckMusicConditions(Context) == false) return;
            await RespondAsync(
                    await MusicEngine.StopAsync(Context.Guild)
                );
        }

        [SlashCommand("skip", "Skip to the next song in the queue.")]
        public async Task SkipMusic()
        {
            if (await CheckMusicConditions(Context) == false) return;
            await RespondAsync(
                    await MusicEngine.SkipTrackAsync(Context.Guild)
                );
        }

        [SlashCommand("toggle", "Pauses and resumes the currently playing song.")]
        public async Task ToggleMusic()
        {
            if (await CheckMusicConditions(Context) == false) return;
            await RespondAsync(
                    await MusicEngine.ToggleAsync(Context.Guild)
                );
        }

        [SlashCommand("leave", "Make the bot leave VC.")]
        public async Task LeaveMusic()
        {
            if (await CheckMusicConditions(Context) == false) return;
            await RespondAsync(
                    await MusicEngine.LeaveAsync(Context.Guild)
                );
        }


        private static async Task<bool> CheckMusicConditions(Discord.Interactions.SocketInteractionContext Context)
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
