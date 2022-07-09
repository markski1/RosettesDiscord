using Dapper;
using Discord;
using Discord.WebSocket;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;  

namespace Rosettes.Database
{
    public interface IGuildRepository
    {
        Task<IEnumerable<Guild>> GetAllGuildsAsync();
        Task<Guild> GetGuildData(SocketGuild guild);
        Task<string> GetGuildSettings(Guild guild);
        Task<bool> CheckGuildExists(ulong guildId);
        Task<bool> InsertGuild(Guild guild);
        Task<bool> UpdateGuild(Guild guild);
        Task<bool> DeleteGuild(Guild guild);
        Task<bool> UpdateGuildRoles(Guild guild);
        Task<ulong> GetGuildDefaultRole(Guild guild);
    }

    public class GuildRepository : IGuildRepository
    {
        private static MySqlConnection DBConnection()
        {
            return new MySqlConnection(Settings.Database.ConnectionString);
        }

        public async Task<IEnumerable<Guild>> GetAllGuildsAsync()
        {
            var db = DBConnection();

            var sql = @"SELECT * FROM guilds";

            try
            {
                return await db.QueryAsync<Guild>(sql, new { });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getallguilds", $"sqlException code {ex.Message}");
                return new List<Guild>();
            }
        }

        public async Task<bool> CheckGuildExists(ulong guildId)
        {
            var db = DBConnection();

            var sql = @"SELECT count(1) FROM guilds WHERE id=@guildId";

            try
            {
                return await db.ExecuteScalarAsync<bool>(sql, new { guildId });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-checkguildexists", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<Guild> GetGuildData(SocketGuild guild)
        {
            var db = DBConnection();

            var sql = @"SELECT id, namecache, members, messages, commands, settings, ownerid FROM guilds WHERE id=@id";

            try
            {
                return await db.QueryFirstOrDefaultAsync<Guild>(sql, new { id = guild.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getguilddata", $"sqlException code {ex.Message}");
                return new Guild(null);
            }
        }

        public async Task<string> GetGuildSettings(Guild guild)
        {
            var db = DBConnection();

            var sql = @"SELECT settings FROM guilds WHERE id=@id";

            try
            {
                return await db.QueryFirstOrDefaultAsync<string>(sql, new { id = guild.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getguildsettings", $"sqlException code {ex.Message}");
                return "1111111111";
            }
        }

        public async Task<ulong> GetGuildDefaultRole(Guild guild)
        {
            var db = DBConnection();

            var sql = @"SELECT defaultrole FROM guilds WHERE id=@id";

            try
            {
                return await db.QueryFirstOrDefaultAsync<ulong>(sql, new { id = guild.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getguildsettings", $"sqlException code {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> InsertGuild(Guild guild)
        {
            var db = DBConnection();

            var sql = @"INSERT INTO guilds (id, namecache, members, messages, commands, settings, ownerid)
                        VALUES(@Id, @NameCache, @Members, @Messages, @Commands, @Settings, @OwnerId)";

            try
            {
                return (await db.ExecuteAsync(sql, new { guild.Id, guild.NameCache, guild.Members, guild.Messages, guild.Commands, guild.Settings, guild.OwnerId })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-insertguild", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateGuild(Guild guild)
        {
            var db = DBConnection();

            var sql = @"UPDATE guilds
                        SET id=@Id, namecache=@NameCache, members=@Members, messages=@Messages, commands=@Commands, ownerid=@OwnerId, autorolesmessage=@AutoRolesMessage
                        WHERE id = @Id";
            try
            {
                return (await db.ExecuteAsync(sql, new { guild.Id, guild.NameCache, guild.Members, guild.Messages, guild.Commands, guild.Settings, guild.OwnerId, guild.AutoRolesMessage })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-updateguild", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateGuildRoles(Guild guild)
        {
            var db = DBConnection();

            var discordGuild = guild.GetDiscordSocketReference();
            if (discordGuild is null) return false;

            var sql = @"DELETE FROM roles WHERE guildid = @Id";
            await db.ExecuteAsync(sql, new { guild.Id });
            
            sql = @"INSERT INTO roles (id, rolename, guildid, color)
                    VALUES (@Id, @Name, @GuildId, @Color)";

            foreach (var role in discordGuild.Roles)
            {
                if (role.IsEveryone) continue;
                if (role.IsManaged) continue;
                try
                {
                    await db.ExecuteAsync(sql, new { role.Id, role.Name, GuildId = guild.Id, Color = role.Color.ToString() });
                }
                catch (Exception ex)
                {
                    Global.GenerateErrorMessage("sql-updateguildroles", $"Failed to update roles - {ex.Message}");
                }
            }

            return true;
        }

        public async Task<bool> DeleteGuild(Guild guild)
        {
            var db = DBConnection();

            var sql = @"DELETE FROM guilds
                        WHERE id = @Id";
            try
            {
                return (await db.ExecuteAsync(sql, new { guild.Id })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-deleteguild", $"sqlException code {ex.Message}");
                return false;
            }
        }
    }
}
