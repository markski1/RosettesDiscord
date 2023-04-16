using Dapper;
using Discord;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Minigame;

namespace Rosettes.Database
{
	public interface IPetRepository
	{
		Task<IEnumerable<Pet>> GetAllPetsAsync();
		Task<bool> CheckPetExists(ulong owner_id, int index);
		Task<bool> InsertPet(Pet pet);
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

			var sql = @"SELECT pet_id, index, owner_id, name, exp, times_pet FROM pets";

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

			var sql = @"SELECT count(1) FROM pets WHERE owner_id=@ownerId AND index=@index";

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

		public async Task<bool> InsertPet(Pet pet)
		{
			var db = DBConnection();

			var sql = @"INSERT INTO pets (pet_id, index, owner_id, name)
						VALUES(@Id, @Index, @ownerId, @Name)";

			try
			{
				return (await db.ExecuteAsync(sql, new { pet.Id, pet.Index, pet.ownerId, pet.Name })) > 0;
			}
			catch (Exception ex)
			{
				Global.GenerateErrorMessage("sql-insertpet", $"sqlException code {ex.Message}");
				return false;
			}
		}

		public async Task<bool> UpdatePet(Pet pet)
		{
			var db = DBConnection();

			var sql = @"UPDATE pets
						SET name=@Name, times_pet=@timesPet
						WHERE pet_id = @Id";

			try
			{
				return (await db.ExecuteAsync(sql, new { pet.Name, pet.timesPet, pet.Id })) > 0;
			}
			catch (Exception ex)
			{
				Global.GenerateErrorMessage("sql-updatepet", $"sqlException code {ex.Message}");
				return false;
			}
		}
	}
}
