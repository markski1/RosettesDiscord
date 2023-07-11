﻿using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using Rosettes.Core;
using Rosettes.Managers;
using Victoria.Node;
using Victoria.Node.EventArgs;
using Victoria.Player;
using Victoria.Responses.Search;

namespace Rosettes.Modules.Engine
{
    public static class MusicEngine
    {
        static readonly NodeConfiguration lavaNodeConfig = new()
        {
            SelfDeaf = true,
            Hostname = Settings.LavaLinkData.Host,
            Port = Settings.LavaLinkData.Port,
            Authorization = (string)Settings.LavaLinkData.Password,
            IsSecure = false
        };

        private static LavaNode? _lavaNode;

        public static void SetMusicEngine(DiscordSocketClient client)
        {
            NullLogger<LavaNode> nothing = new();
            _lavaNode = new(client, lavaNodeConfig, nothing);
            _lavaNode.OnTrackEnd += TrackEnded;
            _lavaNode.OnWebSocketClosed += LavanodeDisconnect;
        }

		private static async Task LavanodeDisconnect(WebSocketClosedEventArg arg)
        {
			if (_lavaNode is not null)
            {
                await _lavaNode.ConnectAsync();
            }
			return;
        }

        public static async Task<string> PlayAsync(SocketGuildUser user, IGuild guild, IVoiceState voiceState, ITextChannel channel, string query)
        {
            if (_lavaNode is null) return "Music playback hasn't initialized yet.";
            if (user.VoiceChannel is null) return "You are not in VC.";

            // because _lavaNode.IsConnected does not return accordingly.
            try
			{
				await _lavaNode.ConnectAsync();
			}
			catch (Exception ex)
			{
				Global.GenerateErrorMessage("MusicEngine-ConnectAsync", $"{ex.Message}");
			}

            if (!_lavaNode.HasPlayer(guild))
            {
                try
                {
                    await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);
                }
                catch (Exception ex)
                {
                    Global.GenerateErrorMessage("MusicEngine-JoinAsync", $"{ex.Message}");
                    return "Error joining the channel, please try again in a bit.";
                }
            }

            try
            {
                if (!_lavaNode.TryGetPlayer(guild, out var player))
                {
                    return "I cannot connect to the channel.";
                }
                
                SearchResponse search;

                search = await _lavaNode.SearchAsync(Uri.IsWellFormedUriString(query, UriKind.Absolute) ? SearchType.Direct : SearchType.YouTube, query);

                if (search.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
                {
                    return $"I was unable to find anything for '{query}'.";
                }

                if (search.Status == SearchStatus.NoMatches) return $"Could not find {query}";

                LavaTrack? track = search.Tracks.FirstOrDefault();

                if (track == null) return "There was an error obtaining a track.";

                if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                {
                    player.Vueue.Enqueue(track);
                    return $"**Added to queue**: `{track.Title}`";
                }

                await player.PlayAsync(track);
                return $"**Now playing**: `{track.Title}`";
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("MusicEngine-PlayAsync", $"{ex.Message}");
                return "There was an error trying to fetch the song, please try again.";
            }
        }

        public static async Task<string> StopAsync(IGuild guild)
        {
            if (_lavaNode is null) return "Music playback hasn't initialized yet.";
            try
            {
                if (!_lavaNode.TryGetPlayer(guild, out var player))
                {
                    return "I cannot connect to the channel.";
                }
                if (player.PlayerState is PlayerState.Playing) await player.StopAsync();
                await _lavaNode.LeaveAsync(player.VoiceChannel);
                return "Playback stopped.";
            }
            catch
            {
                return "Error. Are you sure I'm playing music?";
            }
        }

        public static async Task<string> SkipTrackAsync(IGuild guild)
        {
            if (_lavaNode is null) return "Music playback hasn't initialized yet.";
            try
            {
                if (!_lavaNode.TryGetPlayer(guild, out var player))
                {
                    return "I cannot connect to the channel.";
                }
                if (player == null)
                {
                    return "Error. Are you sure I'm in VC?";
                }

                if (player.Vueue.Count < 1)
                {
                    return "There are no songs left in the queue to skip to.";
                }
                else
                {
                    try
                    {
                        await player.SkipAsync();
                        return $"Skipped to next song: `{player.Track.Title}`";
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
            if (_lavaNode is null) return "Music playback hasn't initialized yet.";
            try
            {
                if (!_lavaNode.TryGetPlayer(guild, out var player))
                {
                    return "I cannot connect to the channel.";
                }
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
            if (_lavaNode is null) return "Music playback hasn't initialized yet.";
            try
            {
                if (!_lavaNode.TryGetPlayer(guild, out var player))
                {
                    return "I cannot connect to the channel.";
                }
                if (player.PlayerState is PlayerState.Playing) await player.StopAsync();
                await _lavaNode.LeaveAsync(player.VoiceChannel);

                return "Left the VC.";
            }
            catch
            {
                return "Error: Not on VC, or something failed.";
            }
        }

        public static async Task TrackEnded(TrackEndEventArg<LavaPlayer<LavaTrack>, LavaTrack> args)
        {
            var playa = args.Player;

            if (!playa.Vueue.TryDequeue(out var queueable)) return;
        
            var playerEmbed = GetPlayer(playa.TextChannel);
            EmbedBuilder embed = await Global.MakeRosettesEmbed();
            embed.Title = "Music player";

            if (queueable is not LavaTrack track)
            {
                embed.Description = $"No songs left to play in queue.";
                await playerEmbed.ModifyAsync(x => x.Embed = embed.Build());
                return;
            }

            await playa.PlayAsync(track);
            if (playerEmbed is not null)
            {
                embed.Description = $"**Playing next song**: `{playa.Track}`";
                await playerEmbed.ModifyAsync(x => x.Embed = embed.Build());
            }
        }

        static readonly Dictionary<IChannel, IUserMessage> channelPlayers = new();

        // we can make a safe assumption that channelPlayers will always contain a player message for a given channel.
        // music cannot start playing without first assigning a message.
        public static IUserMessage GetPlayer(IChannel channel)
        {
            return channelPlayers[channel];
        }

        public static void SetChannelPlayer(ISocketMessageChannel channel, IUserMessage userMessage)
        {
            if (channelPlayers.ContainsKey(channel))
            {
                channelPlayers[channel].ModifyAsync(x => x.Components = new ComponentBuilder().Build());
                channelPlayers[channel] = userMessage;
            }
            else
            {
                channelPlayers.Add(channel, userMessage);
            }
        }
    }
}
