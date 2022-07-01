using Discord;
using Rosettes.Modules.Engine;

namespace Rosettes.Database
{
    public interface IGuildRepository
    {
        Task<IEnumerable<Guild>> GetAllGuildsAsync();
        Task<Guild> GetGuildData(IGuild guild);
        Task<string> GetGuildSettings(Guild guild);
        Task<bool> CheckGuildExists(IGuild guild);
        Task<bool> InsertGuild(Guild guild);
        Task<bool> UpdateGuild(Guild guild);
        Task<bool> DeleteGuild(Guild guild);
    }
}
