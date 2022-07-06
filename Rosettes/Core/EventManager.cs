﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Rosettes.Modules.Commands.Alarms;
using Rosettes.Modules.Engine;
using Victoria;

namespace Rosettes.Core
{
    public static class EventManager
    {
        private static readonly LavaNode _lavaNode = ServiceManager.Provider.GetRequiredService<LavaNode>();
        private static readonly DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        private static readonly CommandService _commandService = ServiceManager.GetService<CommandService>();
        private static bool booting = true;

        public static Task LoadCommands()
        {
            if (booting)
            {
                booting = false;
            }
            else
            {
                return Task.CompletedTask;
            }

            _client.Log += message =>
            {
                Console.WriteLine($"{DateTime.Now} | {message.ToString()}");
                return Task.CompletedTask;
            };
            _commandService.Log += message =>
            {
                Console.WriteLine($"{DateTime.Now} | {message.ToString()}");
                return Task.CompletedTask;
            };

            
            _client.Ready += OnReady;

            _client.JoinedGuild += OnJoinGuild;

            _client.LeftGuild += OnLeftGuild;

            _client.RoleCreated += OnRoleChange;
            _client.RoleDeleted += OnRoleChange;

            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;

            _client.UserJoined += OnUserJoin;

            return Task.CompletedTask;
        }

        // fired when booting
        private static async Task OnReady()
        {
            try
            {
                await _lavaNode.ConnectAsync();
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("OnReady", $"Failed to connect lavanode. {ex.Message}");
            }

            if (Settings.ConnectToDatabase())
            {
                await UserEngine.LoadAllUsersFromDatabase();
                GuildEngine.LoadAllGuildsFromDatabase();
                AutoRolesEngine.SyncWithDatabase();
                AlarmManager.LoadAllAlarmsFromDatabase();
                RequestEngine.Initialize();
            }
            else
            {
                Global.GenerateErrorMessage("OnReady", "Failed to connect to database.");
            }

            CommandEngine.CreateCommandPage();
            Game game = new("$commands", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
            await _client.SetActivityAsync(game);
            Console.WriteLine($"{DateTime.Now} | [READY] - Rosettes is nyow loaded.");
            await _client.SetStatusAsync(UserStatus.Online);
            _client.MessageReceived += OnMessageReceived;
        }

        // fired when a message is received
        private static async Task OnMessageReceived(SocketMessage arg)
        {
            SocketUserMessage? message;
            SocketCommandContext? context;
            try
            {
                message = arg as SocketUserMessage;
                context = new SocketCommandContext(_client, message);
            }
            catch
            {
                // means the message can't be parsed and is likely a system message.
                // rosettes don't handle those, so
                return;
            }

#if !DEBUG
            if (context.Channel.Id == 971467116165353485) return;
#endif

            int argPos = 0;

            // halt if it's not a valid user message.
            if (message == null || message.Author.IsBot)
            {
                return;
            }

            // if it's a valid user message but not a command, pass it over to the message handler.
            if (message.HasCharPrefix(Settings.Prefix, ref argPos))
            {
                await CommandEngine.HandleCommand(context, argPos);
            } else
            {
                await MessageEngine.HandleMessage(context);
            }
        }

        private static async Task<Task> OnJoinGuild(SocketGuild guild)
        {
            Global.GenerateNotification($"Rosettes has joined a new guild. **{guild.Name}**:*{guild.Id}* - {guild.MemberCount} members.");
            await GuildEngine.GetDBGuild(guild);

            return Task.CompletedTask;
        }

        private static Task OnLeftGuild(SocketGuild guild)
        {
            Global.GenerateNotification($"Rosettes has left a guild. **{guild.Name}**:*{guild.Id}* - {guild.MemberCount} members.");

            return Task.CompletedTask;
        }

        private static async Task<Task> OnRoleChange(SocketRole role)
        {
            Guild guild = await GuildEngine.GetDBGuild(role.Guild);
            guild.UpdateRoles();

            return Task.CompletedTask;
        }

        private static async Task<Task> OnUserJoin(SocketGuildUser user)
        {
            if (user is null || user.Guild is null) return Task.CompletedTask;
            var defRole = (await GuildEngine.GetDBGuild(user.Guild)).DefaultRole;
            if (defRole != 0)
            {
                await user.AddRoleAsync(defRole);
            }

            return Task.CompletedTask;
        }

        private static Task OnReactionAdded(Cacheable<IUserMessage, UInt64> message, Cacheable<IMessageChannel, UInt64> channel, SocketReaction reaction)
        {
            var guild = GuildEngine.GetByRoleMessage(channel.Id);
            if (guild is null) return Task.CompletedTask;

            var roles = AutoRolesEngine.GetGuildRolesForEmote(guild.Id, reaction.Emote.Name);

            foreach (var role in roles)
            {
                guild.SetUserRole(reaction.UserId, role.RoleId);
            }

            return Task.CompletedTask;
        }

        private static Task OnReactionRemoved(Cacheable<IUserMessage, UInt64> message, Cacheable<IMessageChannel, UInt64> channel, SocketReaction reaction)
        {
            var guild = GuildEngine.GetByRoleMessage(reaction.MessageId);
            if (guild is null) return Task.CompletedTask;

            var roles = AutoRolesEngine.GetGuildRolesForEmote(guild.Id, reaction.Emote.Name);

            foreach (var role in roles)
            {
                guild.RemoveUserRole(reaction.UserId, role.RoleId);
            }

            return Task.CompletedTask;
        }
    }
}
