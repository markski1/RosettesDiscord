using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Rosettes.Core;
using Rosettes.Modules.Commands.Alarms;
using Rosettes.Modules.Engine;

namespace Rosettes.Managers
{
    public static class EventManager
    {
        private static readonly DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        private static bool booting = true;

        public static Task SetupAsync()
        {
            _client.Log += message =>
            {
                Console.WriteLine($"{DateTime.Now} | {message.ToString()}");
                return Task.CompletedTask;
            };


            _client.Ready += OnReady;

            _client.JoinedGuild += OnJoinGuild;

            _client.LeftGuild += OnLeftGuild;

            _client.RoleCreated += OnRoleChange;
            _client.RoleDeleted += OnRoleChange;
            _client.RoleUpdated += OnRoleChange;

            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;

            _client.UserJoined += OnUserJoin;
            _client.UserLeft += OnUserLeft;

            _client.UserVoiceStateUpdated += JQMonitorEngine.UserVCUpdated;

            return Task.CompletedTask;
        }

        // fired when booting
        private static async Task<Task> OnReady()
        {
            if (booting)
            {
                booting = false;
            }
            else
            {
                return Task.CompletedTask;
            }


            if (Settings.ConnectToDatabase())
            {
                await UserEngine.LoadAllUsersFromDatabase();
                GuildEngine.LoadAllGuildsFromDatabase();
                _ = AutoRolesEngine.SyncWithDatabase();
                AlarmManager.LoadAllAlarmsFromDatabase();
                RequestEngine.Initialize();
            }
            else
            {
                Global.GenerateErrorMessage("OnReady", "Failed to connect to database.");
            }

            Game game = new("Homph", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
            await _client.SetActivityAsync(game);
            await _client.SetStatusAsync(UserStatus.Online);
            _client.MessageReceived += OnMessageReceived;

            // it never would be null if we get this far, but this puts IntelliSense at ease.
            if (ServiceManager.Provider.GetService<InteractionManager>() is InteractionManager _intMan)
            {
                await _intMan.SetupAsync();
            }
            else
            {
                Global.GenerateErrorMessage("startup", "Service provider has not initialized in time????");
            }

            return Task.CompletedTask;
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

            // halt if it's not a valid user message.
            if (message == null || message.Author.IsBot)
            {
                return;
            }

            await MessageEngine.HandleMessage(context);
        }

        private static async Task<Task> OnJoinGuild(SocketGuild guild)
        {
            // Inform of guild join.
            Global.GenerateNotification($"Rosettes has joined a new guild. **{guild.Name}**:*{guild.Id}*");
            await GuildEngine.GetDBGuild(guild);

            // cache users to memory
            foreach (var user in guild.Users)
            {
                if (user is null) continue;
                try
                {
                    await UserEngine.GetDBUser(user);
                }
                catch
                {
                    // just don't crash the rest of the program! We have nothing to handle here
                }
            }

            return Task.CompletedTask;
        }

        private static Task OnLeftGuild(SocketGuild guild)
        {
            Global.GenerateNotification($"Rosettes has left a guild. **{guild.Name}**:*{guild.Id}* - {guild.MemberCount} members.");
            GuildEngine.RemoveGuildFromCache(guild.Id);
            return Task.CompletedTask;
        }

        private static async Task<Task> OnRoleChange(SocketRole role)
        {
            Guild guild = await GuildEngine.GetDBGuild(role.Guild);
            guild.UpdateRoles();

            return Task.CompletedTask;
        }

        private static async Task<Task> OnRoleChange(SocketRole role, SocketRole role1)
        {
            Guild guild = await GuildEngine.GetDBGuild(role.Guild);
            guild.UpdateRoles();

            return Task.CompletedTask;
        }

        private static async Task<Task> OnUserJoin(SocketGuildUser user)
        {
            if (user is null || user.Guild is null) return Task.CompletedTask;

            var dbGuild = await GuildEngine.GetDBGuild(user.Guild);

            // If the guild has set a default role, apply it.
            var defRole = dbGuild.DefaultRole;
            if (defRole != 0)
            {
                await user.AddRoleAsync(defRole);
            }

            if (dbGuild.LogChannel > 0)
            {
                EmbedBuilder embed = await MakeEmbedForUser(user);
                embed.Title = "User joined the server.";
                embed.AddField("Bot:", $"{user.IsBot}");

                dbGuild.SendLogMessage(embed);
            }

            return Task.CompletedTask;
        }

        private static async Task<Task> OnUserLeft(SocketGuild guild, SocketUser user)
        {
            if (user is null || guild is null) return Task.CompletedTask;

            var dbGuild = await GuildEngine.GetDBGuild(guild);

            if (dbGuild.LogChannel > 0)
            {
                EmbedBuilder embed = await MakeEmbedForUser(user);
                embed.Title = "User left the server.";

                dbGuild.SendLogMessage(embed);
            }

            return Task.CompletedTask;
        }

        private static async Task<EmbedBuilder> MakeEmbedForUser(dynamic user)
        {
            if (user is SocketUser or SocketGuildUser)
            {
                EmbedBuilder embed = await Global.MakeRosettesEmbed();
                embed.Description = $"[{user.Username}#{user.Discriminator}]";

                if (user.GetAvatarUrl() != null)
                {
                    embed.ImageUrl = user.GetAvatarUrl();
                }

                return embed;
            }
            else
            {
                return await Global.MakeRosettesEmbed();
            }
        }

        private static async Task<Task> OnReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            // ensure guild is cached and their data can be accessed
            ulong guildid = AutoRolesEngine.GetGuildIdFromMessage(reaction.MessageId);
            if (guildid == 0) return Task.CompletedTask;
            var guild = GuildEngine.GetDBGuildById(guildid);
            if (guild is null) return Task.CompletedTask;

            if (reaction.User.IsSpecified)
            {
                if (reaction.User.Value.IsBot) return Task.CompletedTask;
            }

            // If the message is AutoRoles, apply the relevant role.
            var roles = AutoRolesEngine.GetMessageRolesForEmote(reaction.MessageId, reaction.Emote.Name);

            foreach (var role in roles)
            {
                var success = await guild.SetUserRole(reaction.UserId, role.RoleId);
                if (!success)
                {
                    var cacheChannel = await channel.DownloadAsync();
                    await cacheChannel.SendMessageAsync($"There was an error assigning a role. Check if I have permissions, and make sure my role is higher in the role list than the options.");
                }
            }

            return Task.CompletedTask;
        }

        private static async Task<Task> OnReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            // ensure guild is cached and their data can be accessed
            ulong guildid = AutoRolesEngine.GetGuildIdFromMessage(reaction.MessageId);
            if (guildid == 0) return Task.CompletedTask;
            var guild = GuildEngine.GetDBGuildById(guildid);
            if (guild is null) return Task.CompletedTask;

            if (reaction.User.IsSpecified)
            {
                if (reaction.User.Value.IsBot) return Task.CompletedTask;
            }

            // If the message is AutoRoles, remove the relevant role.
            var roles = AutoRolesEngine.GetMessageRolesForEmote(reaction.MessageId, reaction.Emote.Name);

            foreach (var role in roles)
            {
                var success = await guild.RemoveUserRole(reaction.UserId, role.RoleId);
                if (!success)
                {
                    var cacheChannel = await channel.DownloadAsync();
                    await cacheChannel.SendMessageAsync($"There was an error removing a role. Check if I have permissions, and make sure my role is higher in the role list than the options.");
                }
            }

            return Task.CompletedTask;
        }
    }
}
