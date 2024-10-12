using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Managers;

namespace Rosettes.Modules.Engine.Guild;

public static class GuildEngine
{
    public static List<Guild> GuildCache = new();
    public static readonly GuildRepository _interface = new();

    public static async void SyncWithDatabase()
    {

        foreach (Guild guild in GuildCache.ToList())
        {
            await UpdateGuild(guild);
        }
    }

    public static async Task<bool> UpdateGuild(Guild guild)
    {
        var client = ServiceManager.GetService<DiscordSocketClient>();

        // Guild may have been abandoned.
        if (!client.Guilds.Where(x => x.Id == guild.Id).Any())
        {
            return false;
        }

        // Guild may not be in DB if insert failed on join.
        if (await _interface.CheckGuildExists(guild.Id))
        {
            guild.SelfTest();
            await _interface.UpdateGuild(guild);
            guild.Settings = await _interface.GetGuildSettings(guild);
            guild.DefaultRole = await _interface.GetGuildDefaultRole(guild);
        }
        else
        {
            // handle deletion or memory resets
            SocketGuild? foundGuild = null;
            foreach (SocketGuild aSocketGuild in client.Guilds)
            {
                if (aSocketGuild.Id == guild.Id) foundGuild = aSocketGuild;
            }
            if (foundGuild is null)
            {
                return false;
            }
            else
            {
                await _interface.InsertGuild(guild);
            }
        }
        return true;
    }

    public static void RemoveGuildFromCache(ulong guildid)
    {
        var guildObj = GuildCache.Find(item => item.Id == guildid);
        if (guildObj is not null) GuildCache.Remove(guildObj);
    }

    public static async Task<Guild> LoadGuildFromDatabase(SocketGuild guild)
    {
        Guild getGuild;
        if (await _interface.CheckGuildExists(guild.Id))
        {
            getGuild = await _interface.GetGuildData(guild);
        }
        else
        {
            getGuild = new Guild(guild);
            await _interface.InsertGuild(getGuild);
            getGuild.UpdateRoles();
        }
        if (getGuild.IsValid()) GuildCache.Add(getGuild);
        return getGuild;
    }

    public static async void LoadAllGuildsFromDatabase()
    {
        IEnumerable<Guild> guildCacheTemp;
        guildCacheTemp = await _interface.GetAllGuildsAsync();
        GuildCache = guildCacheTemp.ToList();
        foreach (Guild guild in GuildCache)
        {
            guild.UpdateRoles();
            await guild.UpdateCommands();
        }
    }

    public static async Task<Guild> GetDBGuild(SocketGuild guild)
    {
        try
        {
            return GuildCache.First(item => item.Id == guild.Id);
        }
        catch
        {
            return await LoadGuildFromDatabase(guild);
        }
    }

    // assumes guild is cached! to be used in constructors, where async tasks cannot be awaited.
    public static Guild GetDBGuildById(ulong guild)
    {
        try
        {
            return GuildCache.First(item => item.Id == guild);
        }
        catch
        {
            return new Guild(null);
        }
    }
}


public class Guild
{
    public ulong Id;
    public ulong Members;
    public ulong OwnerId;
    public ulong DefaultRole;
    public ulong LogChannel;
    public ulong FarmChannel;
    public string NameCache;
    private SocketGuild? CachedReference;
    private readonly List<GuildCommand> GuildCommands = new();

    // settings contains 10 characters, each representative of the toggle state of a setting.
    //
    // this is a short sighted and limited approach by design, because rosettes is MEANT TO BE SIMPLE.
    // If we ever need more than 10 settings, we're doing something very wrong.
    //
    // - Char 0: Message Analysis level
    // - Char 1: Deprecated, unused.
    // - Char 2: Random Command toggle
    // - Char 3: Dumb Command toggle
    // - Char 4: Farm Command toggle
    // - Char 5: Monitor voicechat toggle
    //
    public string Settings;

    // normal constructor
    public Guild(SocketGuild? guild)
    {
        if (guild is null)
        {
            Id = 0;
            NameCache = "invalid";
            OwnerId = 0;
        }
        else
        {
            Id = guild.Id;
            NameCache = guild.Name;
            OwnerId = guild.OwnerId;
        }
        CachedReference = guild;
        DefaultRole = 0;
        Members = 0;
        LogChannel = 0;
        FarmChannel = 0;
        Settings = "1111111111";
    }

    // database constructor, used on loading all guilds
    public Guild(ulong id, string namecache, ulong members, string settings, ulong ownerid, ulong defaultrole, ulong logchan, ulong rpgchan)
    {
        Id = id;
        Members = members;
        Settings = settings;
        NameCache = namecache;
        OwnerId = ownerid;
        CachedReference = null;
        DefaultRole = defaultrole;
        LogChannel = logchan;
        FarmChannel = rpgchan;
    }

    public bool IsValid()
    {
        // if guild was created with an Id of 0 it indicates a database failure and this object is invalid.
        return Id != 0;
    }

    public async Task<RestGuild> GetDiscordRestReference()
    {
        DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        var foundGuild = await _client.Rest.GetGuildAsync(Id);
        return foundGuild;
    }

    public SocketGuild GetDiscordSocketReference()
    {
        if (CachedReference is not null) return CachedReference;
        var client = ServiceManager.GetService<DiscordSocketClient>();
        var foundGuild = client.GetGuild(Id);
        CachedReference = foundGuild;
        return CachedReference;
    }

    public void SelfTest()
    {
        SocketGuild? reference = GetDiscordSocketReference();
        if (reference is null)
        {
            return;
        }
        OwnerId = reference.OwnerId;
        NameCache = reference.Name;
        Members = (ulong)reference.MemberCount;
    }

    public bool MessageAnalysis()
    {
        char value = Settings[0];
        return value == '1';
    }

    public bool AllowsRandom()
    {
        char value = Settings[2];
        return value == '1';
    }

    public bool AllowsDumb()
    {
        char value = Settings[3];
        return value == '1';
    }

    public bool AllowsFarm()
    {
        char value = Settings[4];
        return value == '1';
    }

    public bool MonitorsVC()
    {
        char value = Settings[5];
        return value == '1';
    }

    public void ToggleSetting(int id)
    {
        var mutableSettings = Settings.ToCharArray();
        char newSetting;
        if (mutableSettings[id] == '0') newSetting = '1';
        else newSetting = '0';
        mutableSettings[id] = newSetting;
        Settings = new string(mutableSettings);
        _ = GuildEngine._interface.SetGuildSettings(this);
    }

    public async void SetRoleForEveryone(ulong roleid)
    {
        var socketRef = GetDiscordSocketReference();
        if (socketRef is not null)
        {
            await socketRef.DownloadUsersAsync();
            foreach (var user in socketRef.Users)
            {
                await user.AddRoleAsync(roleid);
            }
        }
    }

    // returns either a SocketGuildUser or a RestGuildUser, depending on wether cached or not.
    public async Task<dynamic> GetGuildUser(ulong userid)
    {
        var dref = GetDiscordSocketReference();
        dynamic user;
        user = dref.GetUser(userid);
        // if null, not cached, pick it up through rest
        if (user is null)
        {
            var restSelf = await GetDiscordRestReference();
            user = await restSelf.GetUserAsync(userid);
        }
        return user;
    }

    public async Task<bool> SetUserRole(ulong userid, IEnumerable<AutoRoleEntry> roles)
    {
        var user = await GetGuildUser(userid);
        if (user is not null)
        {
            try
            {
                foreach (var role in roles)
                {
                    await user.AddRoleAsync(role.RoleId);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public async Task<bool> RemoveUserRole(ulong userid, IEnumerable<AutoRoleEntry> roles)
    {
        var user = await GetGuildUser(userid);
        if (user is not null)
        {
            try
            {
                foreach (var role in roles)
                {
                    await user.RemoveRoleAsync(role.RoleId);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public async void UpdateRoles()
    {
        await GuildEngine._interface.UpdateGuildRoles(this);
    }

    public async void SendLogMessage(EmbedBuilder embed)
    {
        var guildref = GetDiscordSocketReference();

        if (guildref is null)
        {
            return;
        }

        SocketTextChannel? chan = guildref.GetTextChannel(LogChannel);

        if (chan is not null)
        {
            await chan.SendMessageAsync(embed: embed.Build());
        }
    }

    public Task ExecuteCommand(SocketSlashCommand arg)
    {
        if (GuildCommands.Any(x => x.name == arg.Data.Name))
        {
            GuildCommand cmd = GuildCommands.First(x => x.name == arg.Data.Name);
            cmd.Execute(arg);
        }

        return Task.CompletedTask;
    }

    public async Task UpdateCommands()
    {
        var cmdsTemp = await GuildEngine._interface.LoadGuildCommands(this);

        var dref = GetDiscordSocketReference();

        if (dref is null) return;

        GuildCommands.Clear();
        await dref.DeleteApplicationCommandsAsync();

        foreach (var cmd in cmdsTemp)
        {
            cmd.BuildSelf();
            GuildCommands.Add(cmd);
        }
    }

    public IUserMessage? cacheSettingsMsg;
}

public class GuildCommand
{
    public ulong guild_id;
    public Guild dbGuild;
    public string name;
    public string description;
    public int action;
    public bool ephemeral;
    public string value;

    public GuildCommand(ulong guildid, string name, string description, int ephemeral, int action, string value)
    {
        guild_id = guildid;
        this.name = name;
        this.action = action;
        this.value = value;
        this.ephemeral = ephemeral != 0;
        this.description = description;
        dbGuild = GuildEngine.GetDBGuildById(guild_id);
    }

    public void BuildSelf()
    {
        SlashCommandBuilder command = new SlashCommandBuilder().
            WithName(name).
            WithDescription(description);

        dbGuild.GetDiscordSocketReference().CreateApplicationCommandAsync(command.Build());
    }

    public async void Execute(SocketInteraction context)
    {
        if (context.User is not SocketGuildUser guildUser)
        {
            Global.GenerateErrorMessage("customcmd-assign", $"Failed to obtain a guild-specific user object!\n Guild: {context.GuildId} ; User: {context.User.Id} ; For command: {name}");
            return;
        }

        switch (action)
        {
            case 0: // message
                await context.RespondAsync(value, ephemeral: ephemeral);
                break;
            case 1: // assign role
                ulong roleId = ulong.Parse(value);
                bool hasRole = guildUser.Roles.Any(x => x.Id == roleId);
                var role = guildUser.Guild.GetRole(roleId);
                try
                {
                    if (hasRole)
                    {
                        await guildUser.RemoveRoleAsync(role);
                        await context.RespondAsync($"Role `{role.Name}` removed.", ephemeral: ephemeral);
                    }
                    else
                    {
                        await guildUser.AddRoleAsync(role);
                        await context.RespondAsync($"Role `{role.Name}` added.", ephemeral: ephemeral);
                    }
                }
                catch
                {
                    await context.RespondAsync("Sorry, I don't have permission to add or remove roles in this guild. Please tell an admin.", ephemeral: true);
                }
                break;
        }
    }
}
