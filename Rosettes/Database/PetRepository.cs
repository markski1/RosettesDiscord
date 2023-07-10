using Dapper;
using Discord;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Minigame;
using System.Data.Common;

namespace Rosettes.Database
{
    public interface IPetRepository
    {
        Task<IEnumerable<Pet>> GetAllPetsAsync();
        Task<bool> CheckPetExists(ulong owner_id, int index);
        Task<int> InsertPet(Pet pet);
        Task<bool> UpdatePet(Pet pet);
    }

    public class PetRepository : IPetRepository
    {
        private static MySqlConnection DBConnection()
        {
            return new MySqlConnection(Settings.Database.ConnectionString);
        }

        public async Task<IEnumerable<Pet>> GetAllPetsAsync()
        {
            var db = DBConnection();

            var sql = @"SELECT pet_id, pet_index, owner_id, pet_name, exp, times_pet, found_date, happiness FROM pets";

            try
            {
                return await db.QueryAsync<Pet>(sql, new { });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-getallpets", $"sqlException code {ex.Message}");
                return new List<Pet>();
            }
        }

        public async Task<bool> CheckPetExists(ulong ownerId, int index)
        {
            var db = DBConnection();

            var sql = @"SELECT count(1) FROM pets WHERE owner_id=@ownerId AND pet_index=@index";

            try
            {
                return await db.ExecuteScalarAsync<bool>(sql, new { ownerId, index });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-checkpetexists", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public async Task<int> InsertPet(Pet pet)
        {
            var db = DBConnection();

            var sql = @"INSERT INTO pets (pet_index, owner_id, pet_name, found_date)
                        VALUES(@Index, @ownerId, @Name, @FoundDate)";

            try
            {
                await db.ExecuteAsync(sql, new { pet.Index, pet.ownerId, Name = pet.GetBareName(), FoundDate = pet.GetFoundDate() });

                sql = @"SELECT pet_id FROM pets WHERE owner_id=@ownerId AND pet_index=@Index";
                var result = await db.QueryAsync<int>(sql,
                                              new { pet.ownerId, pet.Index });
                return result.Single();
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-insertpet", $"sqlException code {ex.Message}");
                return -1;
            }
        }

        public async Task<bool> UpdatePet(Pet pet)
        {
            var db = DBConnection();

            var sql = @"UPDATE pets
                        SET pet_name=@Name, times_pet=@TimesPet, found_date=@FoundDate, happiness=@Happiness
                        WHERE pet_id = @Id";

            try
            {
                return (await db.ExecuteAsync(sql, new { Name = pet.GetBareName(), TimesPet = pet.GetTimesPet(), FoundDate = pet.GetFoundDate(), Happiness = pet.GetHappiness(), pet.Id })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-updatepet", $"sqlException code {ex.Message}");
                return false;
            }
        }
    }
}
