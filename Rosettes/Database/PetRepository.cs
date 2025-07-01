using Dapper;
using Rosettes.Core;
using Rosettes.Modules.Minigame.Pets;

namespace Rosettes.Database;

public abstract class PetRepository
{
    public static async Task<IEnumerable<Pet>> GetAllPetsAsync()
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = "SELECT pet_id, pet_index, owner_id, pet_name, exp, times_pet, found_date, happiness FROM pets";

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

    public static async Task<bool> CheckHasPet(ulong userId, int petSpeciesId)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;
        
        const string sql = "SELECT count(1) FROM pets WHERE owner_id=@userId AND pet_index=@petSpeciesId";

        try
        {
            return await db.QueryFirstOrDefaultAsync<bool>(sql, new { userId, petSpeciesId });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-checkhaspet", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<int> InsertPet(Pet pet)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        var sql = """
                  INSERT INTO pets (pet_index, owner_id, pet_name, found_date)
                  VALUES(@Index, @ownerId, @Name, @FoundDate)
                  """;

        try
        {
            await db.ExecuteAsync(sql, new { pet.Index, pet.OwnerId, Name = pet.GetBareName(), FoundDate = pet.GetFoundDate() });

            sql = "SELECT pet_id FROM pets WHERE owner_id=@ownerId AND pet_index=@Index";
            var result = await db.QueryAsync<int>(sql,
                                          new { pet.OwnerId, pet.Index });
            return result.Single();
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-insertpet", $"sqlException code {ex.Message}");
            return -1;
        }
    }

    public static async Task<bool> UpdatePet(Pet pet)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           UPDATE pets
                           SET pet_name=@Name, times_pet=@TimesPet, found_date=@FoundDate, happiness=@Happiness
                           WHERE pet_id = @Id
                           """;

        try
        {
            return await db.ExecuteAsync(sql, new { Name = pet.GetBareName(), TimesPet = pet.GetTimesPet(), FoundDate = pet.GetFoundDate(), Happiness = pet.GetHappiness(), pet.Id }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-updatepet", $"sqlException code {ex.Message}");
            return false;
        }
    }
}
