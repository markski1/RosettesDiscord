using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;

namespace Rosettes.Modules.Engine
{
    public static class UserEngine
    {
        private static List<User> UserCache = new();
        public static readonly UserRepository inter = new();

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
                await inter.UpdateUser(user);
                user.SyncUpToDate = true;
            }
        }

        public static async Task<User> LoadUserFromDatabase(IUser user)
        {
            User getUser;
            if (await inter.CheckUserExists(user))
            {
                getUser = await inter.GetUserData(user);
            }
            else
            {
                getUser = new User(user.Id);
                _ = inter.InsertUser(getUser);
            }
            if (getUser.IsValid()) UserCache.Add(getUser);
            return getUser;
        }

        public static async void LoadAllUsersFromDatabase()
        {
            IEnumerable<User> userCacheTemp;
            userCacheTemp = await inter.GetAllUsersAsync();
            UserCache = userCacheTemp.ToList();
        }

        public static async Task<User> GetDBUser(IUser user)
        {
            if (!IsUserInCache(user))
            {
                return await LoadUserFromDatabase(user);
            }
            return UserCache.First(item => item.Id == user.Id);
        }

        public static async Task<IEnumerable<User>> GetTopUsers(IGuild guild)
        {
            IEnumerable<User> TopUsers;
            List<User> UsersInGuild = new();

            foreach (IUser currentUser in await guild.GetUsersAsync())
            {
                if (currentUser.IsBot || currentUser.IsWebhook) continue;
                var db_user = await GetDBUser(currentUser);
                UsersInGuild.Add(db_user);
            }

            TopUsers =
                from user in UsersInGuild
                orderby user.GetExperience() descending
                select user;
            TopUsers = TopUsers.Take(10);
            return TopUsers;
        }

        public static async Task<string> GetTopUsersString(IGuild guild)
        {
            int longestNameLenght = 0;
            var text = "";
            var TopUsers = await GetTopUsers(guild);
            // First, figure out the longest name + discriminator combo for formatting.
            foreach (User user in TopUsers)
            {
                string userName = await user.GetName();
                if (userName.Length > longestNameLenght) longestNameLenght = userName.Length;
            }
            // add 1 to it for spacing, and go
            longestNameLenght++;
            foreach (User user in TopUsers)
            {
                string userName = await user.GetName();
                int spacing = longestNameLenght - userName.Length;
                string spacingText = "";
                for (int i = 0; i < spacing; i++)
                {
                    spacingText += " ";
                }
                text += $"{userName}{spacingText}: {user.GetExperience()}\n";
            }
            return text;
        }
    }

    public class User
    {
        public ulong Id { get; set; }
        public bool SyncUpToDate { get; set; }
        private ulong Exp;
        private ulong Currency;
        private int LastUsedCommand;
        private int LastSentMessage;

        public User(ulong id)
        {
            Id = id;
            Exp = 0;
            Currency = 100;
            SyncUpToDate = true;
            LastUsedCommand = 0;
            LastSentMessage = 0;
        }

        // database fetch ctor. "0 references", but in reality dynamically referenced by the UserRepository interface.
        public User(ulong id, ulong exp, ulong currency)
        {
            Id = id;
            Exp = exp;
            Currency = currency;
            SyncUpToDate = true;
            LastUsedCommand = 0;
            LastSentMessage = 0;
        }

        public ulong GetCurrency()
        {
            return Currency;
        }
        public void AddCurrency(ulong amount)
        {
            Currency += amount;
            SyncUpToDate = false;
        }

        public ulong GetExperience()
        {
            return Exp;
        }
        public void AddExperience(ulong amount)
        {
            if (Global.CurrentUnix() > LastSentMessage)
            {
                Exp += amount;
                SyncUpToDate = false;
                LastSentMessage = Global.CurrentUnix() + 9;
            }
        }

        public bool IsValid()
        {
            // if user was created with an Id of 0 it indicates a database failure and this user object is invalid.
            return Id != 0;
        }

        public int GetLevel()
        {
            int level = 0;
            ulong levelUpThreshold = 50;
            ulong experienceLeftAccount = Exp;

            while (experienceLeftAccount > 0)
            {
                if (experienceLeftAccount >= levelUpThreshold)
                {
                    experienceLeftAccount -= levelUpThreshold;
                    levelUpThreshold = (ulong)(levelUpThreshold * 1.1);
                    level++;
                }
                else
                {
                    break;
                }
            }
            return level;
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

        public async Task<string> GetName()
        {
            var userReference = await GetDiscordReference();
            if (userReference is null) return "Failed to get user name.";
            return userReference.Username + "#" + userReference.Discriminator;
        }
    }
}
