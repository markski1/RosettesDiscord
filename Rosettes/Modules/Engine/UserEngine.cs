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

    public static async void SyncWithDatabase()
    {
        try
        {
            foreach (User user in _userCache)
            {
                await UserRepository.UpdateUser(user);
                await Task.Delay(125);
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
            _ = UserRepository.InsertUser(getUser);
        }
        if (getUser.IsValid()) _userCache.Add(getUser);
        return getUser;
    }

    // Return true just for the sake of returning anything to be able to use 'await'.
    // We need to await for all users to be loaded.
    public static async Task LoadAllUsersFromDatabase()
    {
        _userCache = (await UserRepository.GetAllUsersAsync()).ToList();
    }

    public static async Task<User> GetDbUser(IUser user)
    {
        try
        {
            return _userCache.First(item => item.Id == user.Id);
        }
        catch
        {
            return await LoadUserFromDatabase(user);
        }
    }
    
    public static async Task<User> GetDbUserById(ulong userId)
    {
        try
        {
            return _userCache.First(item => item.Id == userId);
        }
        catch
        {
            var user = await GetUserReferenceById(userId);
            if (user is null) return new User(null);
            return await LoadUserFromDatabase(user);
        }
    }

    // assumes user is cached! to be used in constructors, where async tasks cannot be awaited.
    public static User GetCachedDbUserById(ulong user)
    {
        try
        {
            return _userCache.First(item => item.Id == user);
        }
        catch
        {
            return new User(null);
        }
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