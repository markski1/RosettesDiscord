using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;

namespace Rosettes.Modules.Engine
{
    public static class UserEngine
    {
        private static List<User> UserCache = new();
        public static readonly UserRepository _interface = new();

        public static bool IsUserInCache(IUser user)
        {
            User? findUser = null;
            findUser = UserCache.Find(item => item.Id == user.Id);
            return findUser != null;
        }

        public static async void SyncWithDatabase()
        {
            foreach (User user in UserCache)
            {
                if (user.SyncUpToDate) continue;
                // just get the name and don't do anything with it
                // calling the method updates name_cache, which is what we want before saving
                await user.GetName();
                await _interface.UpdateUser(user);
                user.SyncUpToDate = true;
            }
        }

        public static async Task<User> LoadUserFromDatabase(IUser user)
        {
            User getUser;
            if (await _interface.CheckUserExists(user))
            {
                getUser = await _interface.GetUserData(user);
            }
            else
            {
                getUser = new User(user);
                _ = _interface.InsertUser(getUser);
            }
            if (getUser.IsValid()) UserCache.Add(getUser);
            return getUser;
        }

        // return true just for the sake of returning anything in order to be able to use 'await'. We need to await for all users to be loaded.
        public static async Task<bool> LoadAllUsersFromDatabase()
        {
            IEnumerable<User> userCacheTemp;
            userCacheTemp = await _interface.GetAllUsersAsync();
            UserCache = userCacheTemp.ToList();
            return true;
        }

        public static async Task<User> GetDBUser(IUser user)
        {
            if (!IsUserInCache(user))
            {
                return await LoadUserFromDatabase(user);
            }
            return UserCache.First(item => item.Id == user.Id);
        }

        // assumes user is cached! to be used in constructors, where async tasks cannot be awaited.
        public static User GetDBUserById(ulong user)
        {
            return UserCache.First(item => item.Id == user);
        }

        public static async Task<List<User>> GetAllUsersFromGuild(IGuild guild)
        {
            var users = await guild.GetUsersAsync();
            List<User> userList = new();
            if (users is null) return userList;
            foreach (var user in users)
            {
                userList.Add(await GetDBUser(user));
            }
            return userList;
        }
    }

    public class User
    {
        public ulong Id { get; set; }
        // Contains if the user's data in memory has changed since last syncing to database.
        public bool SyncUpToDate { get; set; }

        // timers
        private int LastUsedCommand;
        private int LastFished;


        // Database flags
        private string NameCache = "";

        // normal constructor
        public User(IUser? newUser)
        {
            if (newUser is null)
            {
                Id = 0;
                NameCache = "invalid";
            }
            else
            {
                Id = newUser.Id;
                NameCache = newUser.Username + "#" + newUser.Discriminator;
            }
            SyncUpToDate = true;
            LastUsedCommand = 0;
            LastFished = 0;
        }

        // database constructor, used on loading users
        public User(ulong id, string namecache)
        {
            Id = id;
            SyncUpToDate = true;
            LastUsedCommand = 0;
            LastFished = 0;
            NameCache = namecache;
        }

        public bool IsValid()
        {
            // if user was created with an Id of 0 it indicates a database failure and this user object is invalid.
            return Id != 0;
        }

        public bool CanUseCommand(SocketGuild? guild)
        {
            int change = (guild is null) ? 10 : 3;
            if (Global.CurrentUnix() > LastUsedCommand)
            {
                LastUsedCommand = Global.CurrentUnix() + change;
                return true;
            }
            return false;
        }

        public async Task<IUser> GetDiscordReference()
        {
            var client = ServiceManager.GetService<DiscordSocketClient>();
            return await client.GetUserAsync(Id);
        }

        public async Task<string> GetName(bool full = true)
        {
            var userReference = await GetDiscordReference();
            if (userReference is null) return NameCache;
            string nameGot;
            if (full) nameGot = userReference.Username + "#" + userReference.Discriminator;
            else nameGot = userReference.Username;
            if (nameGot != NameCache)
            {
                NameCache = nameGot;
                SyncUpToDate = false;
            }
            return NameCache;
        }

        // fish stuff

        public bool CanFish()
        {
            if (Global.CurrentUnix() > LastFished)
            {
                LastFished = Global.CurrentUnix() + 3600;
                return true;
            }
            return false;
        }

        // temps for fishing leaderboard
        public int shrimpCache = 0;
        public int sushiCache = 0;
    }
}