﻿using Dapper;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine.Minigame;

namespace Rosettes.Database;

public class PetRepository
{
    public async Task<IEnumerable<Pet>> GetAllPetsAsync()
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

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
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

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
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

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
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

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
