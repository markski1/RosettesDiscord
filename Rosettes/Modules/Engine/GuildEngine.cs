using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;

namespace Rosettes.Modules.Engine
{
    public static class GuildEngine
    {
        private static List<Guild> GuildCache = new();
        public static readonly GuildRepository _interface = new();

        public static bool IsGuildInCache(SocketGuild guild)
        {
            Guild? findGuild = null;
            findGuild = GuildCache.Find(item => item.Id == guild.Id);
            return findGuild != null;
        }

        public static Guild? GetByRoleMessage(ulong channelId)
        {
            foreach (var guild in GuildCache)
            {
                if (guild.AutoRolesMessage == channelId) return guild;
            }
            return null;
        }

        public static async void SyncWithDatabase()
        {
            foreach (Guild guild in GuildCache)
            {
                await UpdateGuild(guild);
            }
        }

        public static async Task<Task> UpdateGuild(Guild guild)
        {
            guild.SelfTest();
            if (await _interface.CheckGuildExists(guild.Id))
            {
                await _interface.UpdateGuild(guild);
                guild.Settings = await _interface.GetGuildSettings(guild);
                guild.DefaultRole = await _interface.GetGuildDefaultRole(guild);
            }
            else
            {
                // handle deletion or memory resets
                var client = ServiceManager.GetService<DiscordSocketClient>();
                SocketGuild? foundGuild = null;
                foreach (SocketGuild aSocketGuild in client.Guilds)
                {
                    if (aSocketGuild.Id == guild.Id) foundGuild = aSocketGuild;
                }
                if (foundGuild is null)
                {
                    GuildCache.Remove(guild);
                }
                else
                {
                    await _interface.InsertGuild(guild);
                }
            }
            return Task.CompletedTask;
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
                await _interface.UpdateGuildRoles(getGuild);
            }
            if (getGuild.IsValid()) GuildCache.Add(getGuild);
            return getGuild;
        }

        public static async Task<bool> LoadAllGuildsFromDatabase()
        {
            IEnumerable<Guild> guildCacheTemp;
            guildCacheTemp = await _interface.GetAllGuildsAsync();
            GuildCache = guildCacheTemp.ToList();
            foreach (Guild guild in GuildCache)
            {
                _ = _interface.UpdateGuildRoles(guild);
            }
            // only returns a bool because I want this to be awaited for
            return true;
        }

        public static async Task<Guild> GetDBGuild(SocketGuild guild)
        {
            if (!IsGuildInCache(guild))
            {
                return await LoadGuildFromDatabase(guild);
            }
            return GuildCache.First(item => item.Id == guild.Id);
        }

        // assumes user is cached! to be used in constructors, where async tasks cannot be awaited.
        public static Guild GetDBGuildById(ulong guild)
        {
            return GuildCache.First(item => item.Id == guild);
        }
    }


    public class Guild
    {
        public ulong Id;
        public ulong Messages;
        public ulong Commands;
        public ulong Members;
        public ulong OwnerId;
        public ulong DefaultRole;
        public ulong AutoRolesMessage;
        public SocketGuild? CachedReference;
        public string NameCache;

        // settings contains 10 characters, each representative of the toggle state of a setting.
        //
        // this is a short sighted and limited approach by design, because rosettes is MEANT TO BE SIMPLE.
        // If we ever need more than 10 settings, we're doing something very wrong.
        //
        // - Char 0: Message Analysis level
        // - Char 1: Music Command toggle
        // - Char 2: Random Command toggle
        // - Char 3: Dumb Command toggle
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
            AutoRolesMessage = 0;
            DefaultRole = 0;
            Messages = 0;
            Members = 0;
            Settings = "2111111111";
        }

        // database constructor, used on loading all guilds
        public Guild(ulong id, string namecache, ulong members, ulong messages, ulong commands, string settings, ulong ownerid, ulong defaultrole, ulong autorolesmessage)
        {
            Id = id;
            Messages = messages;
            Members = members;
            Commands = commands;
            Settings = settings;
            NameCache = namecache;
            OwnerId = ownerid;
            CachedReference = null;
            DefaultRole = defaultrole;
            AutoRolesMessage = autorolesmessage;
        }

        public bool IsValid()
        {
            // if guild was created with an Id of 0 it indicates a database failure and this user object is invalid.
            return Id != 0;
        }

        public SocketGuild? GetDiscordReference()
        {
            if (CachedReference is not null) return CachedReference;
            var client = ServiceManager.GetService<DiscordSocketClient>();
            var foundGuild = client.GetGuild(Id);
            if (foundGuild is not null)
            {
                CachedReference = foundGuild;
            }
            else
            {
                foreach (var guild in client.Guilds)
                {
                    if (guild.Id == Id)
                    {
                        CachedReference = guild;
                        break;
                    }
                }
            }
            return CachedReference;
        }

        public void SelfTest()
        {
            SocketGuild? reference = GetDiscordReference();
            if (reference is null)
            {
                Global.GenerateErrorMessage("guild-selftest", $"Failed to find reference for {NameCache}");
                return;
            }
            OwnerId = reference.OwnerId;
            NameCache = reference.Name;
            Members = (ulong)reference.MemberCount;
        }

        public int MessageAnalysis()
        {
            char value = Settings[0];
            return (int)(value - '0');
        }
        public bool AllowsMusic()
        {
            char value = Settings[1];
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


        public async void SetRoleForEveryone(ulong roleid)
        {
            var dref = GetDiscordReference();
            if (dref is null) return;
            try
            {
                foreach (var user in dref.Users)
                {
                    await user.AddRoleAsync(roleid);
                }
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("guild-setroleforeveryone", $"{ex}");
                return;
            }
        }

        public SocketGuildUser? GetSocketUser(ulong userid)
        {
            var dref = GetDiscordReference();
            if (dref is null) return null;
            return dref.GetUser(userid);
        }

        public async void UpdateRoles()
        {
            await GuildEngine._interface.UpdateGuildRoles(this);
        }
    }
}
