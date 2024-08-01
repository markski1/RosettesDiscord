using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Managers;
using Rosettes.Modules.Engine.Minigame;

namespace Rosettes.Modules.Engine;

public static class UserEngine
{
    private static List<User> UserCache = [];
    public static readonly UserRepository _interface = new();

    public static async void SyncWithDatabase()
    {
        foreach (User user in UserCache)
        {
            await _interface.UpdateUser(user);
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
        try
        {
            return UserCache.First(item => item.Id == user.Id);
        }
        catch
        {
            return await LoadUserFromDatabase(user);
        }
    }

    // assumes user is cached! to be used in constructors, where async tasks cannot be awaited.
    public static User GetDBUserById(ulong user)
    {
        try
        {
            return UserCache.First(item => item.Id == user);
        }
        catch
        {
            return new User(null);
        }
    }

    public static async Task<IUser> GetUserReferenceByID(ulong id)
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
            userList.Add(await GetDBUser(user));
        }
        return userList;
    }

    public static async Task<User> GetUserByRosettesKey(string rosettes_key)
    {
        ulong? result = await _interface.GetUserByRosettesKey(rosettes_key);

        if (result is null) return new User(null);

        return GetDBUserById((ulong)result);
    }
}

public class User
{
    public ulong Id { get; }
    public int MainPet { get; set; }
    public int Exp { get; set; }
    public string NameCache { get; set; } = "";
    public string Username { get; set; } = "";

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
            if (newUser.GlobalName is not null)
                NameCache = newUser.GlobalName;
            else
                NameCache = newUser.Username;
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
        // if user was created with an Id of 0 it indicates a database failure and this user object is invalid.
        return Id != 0;
    }

    public async Task<IUser> GetDiscordReference()
    {
        return await UserEngine.GetUserReferenceByID(Id);
    }

    public async Task<string> GetName()
    {
        var userReference = await GetDiscordReference();
        if (userReference is null) return NameCache;

        string nameGot;
        if (userReference.GlobalName is not null)
        {
            nameGot = userReference.GlobalName;
        }
        else
        {
            nameGot = userReference.Username;
        }
        if (nameGot != NameCache)
        {
            NameCache = nameGot;
        }
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
        if (Global.CurrentUnix() > LastFished)
        {
            LastFished = Global.CurrentUnix() + 3600;
            return true;
        }
        return false;
    }

    public int GetFishTime()
    {
        return LastFished;
    }

    public void SetPet(int id)
    {
        MainPet = id;
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