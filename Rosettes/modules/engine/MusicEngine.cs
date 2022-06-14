using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Rosettes.core;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Responses.Search;

namespace Rosettes.modules.engine
{
    public static class MusicEngine
    {
        private static readonly LavaNode _lavaNode = ServiceManager.Provider.GetRequiredService<LavaNode>();
    
        public static async Task<string> PlayAsync(SocketGuildUser user, IGuild guild, IVoiceState voiceState, ITextChannel channel, string query)
        {
            if (user.VoiceChannel is null) return "You are not in VC.";

            if (!_lavaNode.HasPlayer(guild))
            {
                try
                {
                    await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);
                }
                catch (Exception ex)
                {
                    Global.GenerateErrorMessage("MusicEngine-JoinAsync", $"{ex.Message}");
                    return "Error joining the channel.";
                }
            }

            try
            {
                var player = _lavaNode.GetPlayer(guild);

                SearchResponse search;
                if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
                {
                    search = await _lavaNode.SearchAsync(SearchType.YouTubeMusic, query);
                }
                else
                {
                    search = await _lavaNode.SearchYouTubeAsync(query);
                }

                if (search.Status == SearchStatus.NoMatches) return $"Could not find {query}";

                LavaTrack? track = search.Tracks.FirstOrDefault();

                if (track == null) return "There was an error obtaining a track.";

                if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                {
                    player.Queue.Enqueue(track);
                    return $"{track.Title} - Added to queue.";
                }

                await player.PlayAsync(track);
                return $"{track.Title} - Now playing.";
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("MusicEngine-PlayAsync", $"{ex.Message}");
                return "There was an error trying to fetch the song.";
            }
        }

        public static async Task<string> StopAsync(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (player.PlayerState is PlayerState.Playing) await player.StopAsync();
                return "Playback stopped.";
            }
            catch
            {
                return "Error. Are you sure I'm playing music?";
            }
        }

        public static async Task<string> SkipTrackAsync(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (player == null)
                {
                    return "Error. Are you sure I'm in VC?";
                }

                if (player.Queue.Count < 1)
                {
                    return "There are no songs left in the queue to skip to.";
                }
                else
                {
                    try
                    {
                        await player.SkipAsync();
                        return "Skipped to next song.";
                    }
                    catch
                    {
                        return "There was an error trying to skip to the next song.";
                    }
                }
            }
            catch
            {
                return "There was an error trying to skip to the next song.";
            }
        }

        public static async Task<string> ToggleAsync(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (player.PlayerState is PlayerState.Playing)
                {
                    await player.PauseAsync();
                    return "Playback paused.";
                }
                if (player.PlayerState is PlayerState.Paused)
                {
                    await player.ResumeAsync();
                    return "Playback resumed.";
                }
                
                return "No song is playing.";
            }
            catch
            {
                return "There was an error trying to toggle.";
            }
        }

        public static async Task<string> LeaveAsync(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (player.PlayerState is PlayerState.Playing) await player.StopAsync();
                await _lavaNode.LeaveAsync(player.VoiceChannel);

                return "Left the VC.";
            }
            catch
            {
                return "Error: Not on VC, or something failed.";
            }
        }

        public static async Task TrackEnded(TrackEndedEventArgs args)
        {
            var playa = args.Player;

            if (!playa.Queue.TryDequeue(out var queueable)) return;
        
            if (queueable is not LavaTrack track)
            {
                await playa.TextChannel.SendMessageAsync("The next item in the queue is invalid.");
                return;
            }

            await playa.PlayAsync(track);
            await playa.TextChannel.SendMessageAsync($"Playing: {track.Title}");
        }
    }
}
