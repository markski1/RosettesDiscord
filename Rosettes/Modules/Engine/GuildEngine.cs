using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;

namespace Rosettes.Modules.Engine
{
    public static class GuildEngine
    {


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
        public Guild(ulong id, ulong messages, ulong commands, ulong members, string settings, string namecache)
        {
            Id = id;
            Messages = messages;
            Members = members;
            Commands = commands;
            Settings = settings;
            NameCache = namecache;
        }


    }
}
