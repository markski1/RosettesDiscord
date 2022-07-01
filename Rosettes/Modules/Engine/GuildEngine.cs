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

        public static bool IsGuildInCache(IGuild guild)
        {
            Guild? findGuild = null;
            findGuild = GuildCache.Find(item => item.Id == guild.Id);
            return findGuild != null;
        }

        public static async void SyncWithDatabase()
        {
            foreach (Guild guild in GuildCache)
            {
                await _interface.UpdateGuild(guild);
                guild.Settings = await _interface.GetGuildSettings(guild);
            }
        }

        public static async Task<Guild> LoadGuildFromDatabase(IGuild guild)
        {
            Guild getGuild;
            if (await _interface.CheckGuildExists(guild))
            {
                getGuild = await _interface.GetGuildData(guild);
            }
            else
            {
                getGuild = new Guild(guild);
                _ = _interface.InsertGuild(getGuild);
            }
            if (getGuild.IsValid()) GuildCache.Add(getGuild);
            return getGuild;
        }

        // return might seem useless but we need to AWAIT for all users to be loaded.
        public static async Task<bool> LoadAllGuildsFromDatabase()
        {
            IEnumerable<Guild> guildCacheTemp;
            guildCacheTemp = await _interface.GetAllGuildsAsync();
            GuildCache = guildCacheTemp.ToList();
            return true;
        }

        public static async Task<Guild> GetDBGuild(IGuild guild)
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
        public string NameCache;

        // settings contains 10 characters, each representative of the toggle state of a setting.
        //
        // this is a short sighted and limited approach by design, because rosettes is MEANT TO BE SIMPLE.
        // If we ever need more than 10 settings, we're doing something very wrong.
        public string Settings;

        // normal constructor
        public Guild(IGuild? guild)
        {
            if (guild is null)
            {
                Id = 0;
                NameCache = "invalid";
            }
            else
            {
                Id = guild.Id;
                NameCache = guild.Name;
            }
            Messages = 0;
            Members = 0;
            Settings = "1111111111";
            
        }

        // database constructor, used on loading users
        public Guild(ulong id, string namecache, ulong members, ulong messages, ulong commands, string settings)
        {
            Id = id;
            Messages = messages;
            Members = members;
            Commands = commands;
            Settings = settings;
            NameCache = namecache;
        }

        public bool IsValid()
        {
            // if guild was created with an Id of 0 it indicates a database failure and this user object is invalid.
            return Id != 0;
        }
    }
}
