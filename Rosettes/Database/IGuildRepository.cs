using Discord;
using Discord.WebSocket;
using Rosettes.Modules.Engine;

namespace Rosettes.Database
{
    public interface IGuildRepository
    {
        Task<IEnumerable<Guild>> GetAllGuildsAsync();
        Task<Guild> GetGuildData(SocketGuild guild);
        Task<string> GetGuildSettings(Guild guild);
        Task<bool> CheckGuildExists(SocketGuild guild);
        Task<bool> InsertGuild(Guild guild);
        Task<bool> UpdateGuild(Guild guild);
        Task<bool> DeleteGuild(Guild guild);
    }
}
