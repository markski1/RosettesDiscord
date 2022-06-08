using Discord;
using Rosettes.modules.engine;

namespace Rosettes.database
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User> GetUserData(IUser user);
        Task<bool> CheckUserExists(IUser user);
        Task<bool> InsertUser(User user);
        Task<bool> UpdateUser(User user);
        Task<bool> DeleteUser(User user);
    }
}
