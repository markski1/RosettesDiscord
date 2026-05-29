using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Managers;
using Rosettes.Modules.Minigame.Pets;

namespace Rosettes.Modules.Engine;

public static class UserEngine
{
    private static List<User> _userCache = [];
    private static readonly Lock CacheLock = new();

    public static async Task SyncWithDatabase()
    {
        List<User> snapshot;
        lock (CacheLock)
        {
            snapshot = [.._userCache];
        }

        try
        {
            // Snapshot dirty users before iterating so concurrent cache additions don't cause
            // a 'collection modified during enumeration' exception.
            var dirtyUsers = _userCache.Where(u => u.Dirty).ToList();

            foreach (User user in dirtyUsers)
            {
                // Names are kept fresh in GetDbUser (no Discord API calls needed here).
                if (await UserRepository.UpdateUser(user))
                {
                    user.Dirty = false;
                }
            }
        }
        catch (Exception e)
        {
            Global.GenerateErrorMessage("uengine-updt", $"Exception while updating users: {e.Message}");
        }
    }

    private static async Task<User> LoadUserFromDatabase(IUser user)
    {
        User getUser;
        if (await UserRepository.CheckUserExists(user))
        {
            getUser = await UserRepository.GetUserData(user);
        }
        else
        {
            getUser = new User(user);
            await UserRepository.InsertUser(getUser);
        }
        if (getUser.IsValid())
        {
            lock (CacheLock)
            {
                _userCache.Add(getUser);
            }
        }
        return getUser;
    }

    public static async Task LoadAllUsersFromDatabase()
    {
        var loaded = (await UserRepository.GetAllUsersAsync()).ToList();
        lock (CacheLock)
        {
            _userCache = loaded;
        }
    }

    public static async Task<User> GetDbUser(IUser user)
    {
        var cached = _userCache.FirstOrDefault(item => item.Id == user.Id);
        if (cached is not null)
        {
            // We already have the live Discord reference. Refresh the name cache for free,
            // with no extra API call. Any change will be written on the next sync cycle.
            var newName = user.GlobalName ?? user.Username;
            if (cached.NameCache != newName)
            {
                cached.NameCache = newName;
                cached.Dirty = true;
            }
            if (user.Username is not null && cached.Username != user.Username)
            {
                cached.Username = user.Username;
                cached.Dirty = true;
            }
            return cached;
        }
        return await LoadUserFromDatabase(user);
    }

    public static async Task<User> GetDbUserById(ulong userId)
    {
        var cached = _userCache.FirstOrDefault(item => item.Id == userId);
        if (cached is not null) return cached;

        var user = await GetUserReferenceById(userId);
        if (user is null) return new User(null);
        return await LoadUserFromDatabase(user);
    }

    // assumes user is cached! to be used in constructors, where async tasks cannot be awaited.
    public static User GetCachedDbUserById(ulong userId)
    {
        return _userCache.FirstOrDefault(item => item.Id == userId) ?? new User(null);
    }

    public static async Task<IUser?> GetUserReferenceById(ulong id)
    {
        var client = ServiceManager.GetService<DiscordSocketClient>();
        IUser user = client.GetUser(id);
        user ??= await client.GetUserAsync(id);
        return user;
    }

    public static async Task<List<User>> GetAllUsersFromGuild(IGuild guild)
    {
        var users = await guild.GetUsersAsync();
        List<User> userList = [];
        if (users is null) return userList;
        foreach (var user in users)
        {
            userList.Add(await GetDbUser(user));
        }
        return userList;
    }

    public static async Task<User> GetUserByRosettesKey(string rosettesKey)
    {
        ulong? result = await UserRepository.GetUserByRosettesKey(rosettesKey);

        return result is null ? new User(null) : await GetDbUserById((ulong)result);
    }
}

public class User
{
    public ulong Id { get; }
    public int MainPet { get; set; }
    public int Exp { get; set; }
    public string NameCache { get; set; }
    public string Username { get; set; }

    public bool Dirty { get; set; }

    // timers
    private int LastFished { get; set; }

    // normal constructor
    public User(IUser? newUser)
    {
        if (newUser is null)
        {
            Id = 0;
            NameCache = "invalid";
            Username = "invalid";
        }
        else
        {
            Id = newUser.Id;
            NameCache = newUser.GlobalName ?? newUser.Username;
            Username = newUser.Username;
        }
        LastFished = 0;
        MainPet = 0;
        Exp = 0;
    }

    // database constructor, used on loading users
    public User(ulong id, string username, string namecache, int exp, int mainpet)
    {
        Id = id;
        LastFished = 0;
        Username = username;
        NameCache = namecache;
        MainPet = mainpet;
        Exp = exp;
    }

    public bool IsValid()
    {
        // if user was created with an id of 0 it indicates a database failure and this user object is invalid.
        return Id != 0;
    }

    public async Task<IUser?> GetDiscordReference()
    {
        return await UserEngine.GetUserReferenceById(Id);
    }

    public async Task<bool> SendDirectMessage(string message)
    {
        var userRef = await UserEngine.GetUserReferenceById(Id);

        try
        {
            await userRef.SendMessageAsync(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetName()
    {
        var userReference = await GetDiscordReference();
        if (userReference is null) return NameCache;

        var nameGot = userReference.GlobalName ?? userReference.Username;
        NameCache = nameGot;
        return NameCache;
    }

    public async Task<string> GetUsername()
    {
        var userReference = await GetDiscordReference();
        if (userReference is null) return Username;

        string nameGot = "invalid";
        if (userReference.Username is not null)
        {
            nameGot = userReference.Username;
        }
        if (nameGot != Username)
        {
            Username = nameGot;
        }
        return Username;
    }

    // farm stuff

    public bool CanFish()
    {
        if (Global.CurrentUnix() <= LastFished) return false;
        LastFished = Global.CurrentUnix() + 3600;
        return true;
    }

    public int GetFishTime()
    {
        return LastFished;
    }

    public void SetPet(int id)
    {
        if (MainPet == id) return;
        MainPet = id;
        Dirty = true;
        if (MainPet > 0)
        {
            _ = PetEngine.EnsurePetExists(Id, MainPet);
        }
    }

    // returns 0 unless adding exp resulted in a level up, in which case returns the level.
    public string AddExp(int amount)
    {
        int level = GetLevel();
        Exp += amount;
        Dirty = true;
        if (GetLevel() > level)
        {
            return $"+{amount} exp, leveled up";
        }
        return $"+{amount} exp";
    }

    public int GetLevel()
    {
        float count = Exp;
        float requirement = 100.0f;
        int level = 1;

        while (count > 0.9f)
        {
            if (count >= requirement)
            {
                count -= requirement;
                requirement *= 1.1f;
                level += 1;
            }
            else break;
        }
        return level;
    }
}
