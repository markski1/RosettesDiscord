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
        if (await GuildRepository.CheckGuildExists(guild.Id))
        {
            guild.SelfTest();
            await GuildRepository.UpdateGuild(guild);
            guild.Settings = await GuildRepository.GetGuildSettings(guild);
            guild.DefaultRole = await GuildRepository.GetGuildDefaultRole(guild);
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
                await GuildRepository.InsertGuild(guild);
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
        if (await GuildRepository.CheckGuildExists(guild.Id))
        {
            getGuild = await GuildRepository.GetGuildData(guild);
        }
        else
        {
            getGuild = new Guild(guild);
            await GuildRepository.InsertGuild(getGuild);
            getGuild.UpdateRoles();
        }
        if (getGuild.IsValid()) GuildCache.Add(getGuild);
        return getGuild;
    }

    public static async void LoadAllGuildsFromDatabase()
    {
        IEnumerable<Guild> guildCacheTemp;
        guildCacheTemp = await GuildRepository.GetAllGuildsAsync();
        GuildCache = guildCacheTemp.ToList();
        foreach (Guild guild in GuildCache)
        {
            guild.UpdateRoles();
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

    public SocketGuild? GetDiscordSocketReference()
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
        _ = GuildRepository.SetGuildSettings(this);
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
        var user = dref.GetUser(userid);
        dref.GetUsersAsync();
        if (user is not null) return user;

        // user not cached
        var restSelf = await GetDiscordRestReference();
        var restUser = await restSelf.GetUserAsync(userid);
        return restUser;
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
        await GuildRepository.UpdateGuildRoles(this);
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

    public IUserMessage? cacheSettingsMsg;
}