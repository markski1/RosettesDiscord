using Dapper;
using Discord;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Database
{
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

        public async Task<bool> CheckGuildExists(IGuild guild)
        {
            var db = DBConnection();

            var sql = @"SELECT count(1) FROM guilds WHERE id=@Id";

            try
            {
                return await db.ExecuteScalarAsync<bool>(sql, new { guild.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-checkguildexists", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<Guild> GetGuildData(IGuild guild)
        {
            var db = DBConnection();

            var sql = @"SELECT id, namecache, members, messages, commands, settings FROM guilds WHERE id=@id";

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

        public async Task<bool> InsertGuild(Guild guild)
        {
            var db = DBConnection();

            var sql = @"INSERT INTO guilds (id, namecache, members, messages, commands, settings)
                        VALUES(@Id, @NameCache, @Members, @Messages, @Commands, @Settings)";

            try
            {
                return (await db.ExecuteAsync(sql, new { guild.Id, guild.NameCache, guild.Members, guild.Messages, guild.Commands, guild.Settings })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-insertguild", $"sqlException code {ex.Message}");
                return false;
            }
        }

        // ABAJO TODO

        public async Task<bool> UpdateGuild(Guild guild)
        {
            var db = DBConnection();

            var sql = @"UPDATE guilds
                        SET id=@Id, namecache=@NameCache, members=@Members, messages=@Messages, commands=@Commands, settings=@Settings
                        WHERE id = @Id";
            try
            {
                return (await db.ExecuteAsync(sql, new { guild.Id, guild.NameCache, guild.Members, guild.Messages, guild.Commands, guild.Settings })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-updateguild", $"sqlException code {ex.Message}");
                return false;
            }
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
