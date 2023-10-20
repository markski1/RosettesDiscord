using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Modules.Commands.Alarms;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Guild;
using Rosettes.Modules.Engine.Minigame;

namespace Rosettes.Managers;

public static class EventManager
{
    private static readonly DiscordSocketClient Client = ServiceManager.GetService<DiscordSocketClient>();
    private static bool _booting = true;

    public static Task SetupAsync()
    {
        Client.Log += message =>
        {
            Console.WriteLine($"{DateTime.Now} | {message.ToString()}");
            return Task.CompletedTask;
        };


        Client.Ready += OnReady;

        Client.JoinedGuild += OnJoinGuild;

        Client.LeftGuild += OnLeftGuild;

        Client.RoleCreated += OnRoleChange;
        Client.RoleDeleted += OnRoleChange;
        Client.RoleUpdated += OnRoleChange;

        Client.ReactionAdded += OnReactionAdded;
        Client.ReactionRemoved += OnReactionRemoved;

        Client.UserJoined += OnUserJoin;
        Client.UserLeft += OnUserLeft;

        Client.UserVoiceStateUpdated += JQMonitorEngine.UserVCUpdated;

        return Task.CompletedTask;
    }

    // fired when booting
    private static async Task<Task> OnReady()
    {
        if (_booting)
        {
            _booting = false;
        }
        else
        {
            return Task.CompletedTask;
        }


        if (Settings.LoadDatabaseObj())
        {
            await UserEngine.LoadAllUsersFromDatabase();
            _ = Task.Run(async () =>
            {
                PetEngine.LoadAllPetsFromDatabase();
                GuildEngine.LoadAllGuildsFromDatabase();
                AlarmManager.LoadAllAlarmsFromDatabase();
                RequestManager.Initialize();
                await AutoRolesEngine.SyncWithDatabase();
            });
        }
        else
        {
            Global.GenerateErrorMessage("OnReady", "Failed to connect to database.");
        }

        Game game = new("Homph", type: ActivityType.Playing, flags: ActivityProperties.Join, details: "mew wew");
        await Client.SetActivityAsync(game);
        await Client.SetStatusAsync(UserStatus.Online);
        Client.MessageReceived += OnMessageReceived;

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
    private static Task OnMessageReceived(SocketMessage arg)
    {
        _ = Task.Run(async () =>
        {
            SocketUserMessage? message;
            SocketCommandContext? context;
            try
            {
                message = arg as SocketUserMessage;
                context = new SocketCommandContext(Client, message);
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

            await MessageManager.HandleMessage(context);
        });

        return Task.CompletedTask;
    }

    private static async Task<Task> OnJoinGuild(SocketGuild guild)
    {
        await GuildEngine.GetDBGuild(guild);

        // cache users to memory
        foreach (var user in guild.Users)
        {
            if (user is null) continue;
            try
            {
                await UserEngine.GetDBUser(user);
            }
            catch (Exception e)
            {
                Global.GenerateErrorMessage("OnJoinGuild", $"Error caching user {user.Username} in guild {guild.Name} ```{e.Message}```");
            }
        }

        return Task.CompletedTask;
    }

    private static Task OnLeftGuild(SocketGuild guild)
    {
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
        if (user.Guild is null) return Task.CompletedTask;

        var dbGuild = await GuildEngine.GetDBGuild(user.Guild);

        // If the guild has set a default role, apply it.
        var defRole = dbGuild.DefaultRole;
        if (defRole != 0)
        {
            await user.AddRoleAsync(defRole);
        }

        if (dbGuild.LogChannel <= 0) return Task.CompletedTask;
        
        EmbedBuilder embed = await MakeEmbedForUser(user);
        embed.Title = "User joined the server.";
        embed.AddField("Bot:", $"{user.IsBot}");

        dbGuild.SendLogMessage(embed);

        return Task.CompletedTask;
    }

    private static async Task<Task> OnUserLeft(SocketGuild guild, SocketUser user)
    {
        var dbGuild = await GuildEngine.GetDBGuild(guild);

        if (dbGuild.LogChannel <= 0) return Task.CompletedTask;
        
        EmbedBuilder embed = await MakeEmbedForUser(user);
        embed.Title = "User left the server.";

        dbGuild.SendLogMessage(embed);

        return Task.CompletedTask;
    }

    // ONLY used for Join and Quit notifications. NOT interchangeable with Global's MakeRosettesEmbed.
    private static async Task<EmbedBuilder> MakeEmbedForUser(dynamic user)
    {
        if (user is SocketUser or SocketGuildUser)
        {
            EmbedBuilder embed = await Global.MakeRosettesEmbed();
            embed.Description = $"[{user.Username}]";

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
        if (reaction.User.IsSpecified)
        {
            if (reaction.User.Value.IsBot) return Task.CompletedTask;
        }

        // ensure guild is cached and their data can be accessed
        ulong guildid = AutoRolesEngine.GetGuildIdFromMessage(reaction.MessageId);
        if (guildid == 0) return Task.CompletedTask;
        var guild = GuildEngine.GetDBGuildById(guildid);

        // If the message is AutoRoles, apply the relevant role.
        var roles = AutoRolesEngine.GetMessageRolesForEmote(reaction.MessageId, reaction.Emote.Name);
        var success = await guild.SetUserRole(reaction.UserId, roles);
        
        if (success) return Task.CompletedTask;
        
        var cacheChannel = await channel.DownloadAsync();
        await cacheChannel.SendMessageAsync($"There was an error assigning a role. Check if I have permissions, and make sure my role is higher in the role list than the options.");
        return Task.CompletedTask;
    }

    private static async Task<Task> OnReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (reaction.User.IsSpecified)
        {
            if (reaction.User.Value.IsBot) return Task.CompletedTask;
        }

        // ensure guild is cached and their data can be accessed
        ulong guildid = AutoRolesEngine.GetGuildIdFromMessage(reaction.MessageId);
        if (guildid == 0) return Task.CompletedTask;
        var guild = GuildEngine.GetDBGuildById(guildid);

        // If the message is AutoRoles, remove the relevant role.
        var roles = AutoRolesEngine.GetMessageRolesForEmote(reaction.MessageId, reaction.Emote.Name);

        var success = await guild.RemoveUserRole(reaction.UserId, roles);
        if (success) return Task.CompletedTask;
        
        var cacheChannel = await channel.DownloadAsync();
        await cacheChannel.SendMessageAsync($"There was an error removing a role. Check if I have permissions, and make sure my role is higher in the role list than the options.");
        return Task.CompletedTask;
    }
}
