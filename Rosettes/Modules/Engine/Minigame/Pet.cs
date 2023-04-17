using Discord.WebSocket;
using Discord;
using Rosettes.Core;
using System.Linq;

namespace Rosettes.Modules.Engine.Minigame
{
	public class Pet
	{
		public int Id;
		public int Index;
		public int timesPet;
		public int Exp;
		public ulong ownerId;
		public string Name;

		public int LastPet = 0;

		public Pet(int index, ulong owner_id, string name)
		{
			Index = index;
			ownerId = owner_id;
			Name = name;
			timesPet = 0;
			Exp = 0;
		}

		public Pet(int pet_id, int pet_index, ulong owner_id, string pet_name, int exp, int times_pet)
		{
			Id = pet_id;
			Index = pet_index;
			ownerId = owner_id;
			Name = pet_name;
			timesPet = times_pet;
			Exp = exp;
		}

		public string GetEmoji()
		{
			return PetEngine.PetEmojis(Index);
		}

		public string GetName()
		{
			if (Name != "[not named]")
			{
				return $"{PetEngine.PetEmojis(Index)} {Name}";
			}
			else
			{
				return PetEngine.PetNames(Index);
			}
		}

		public bool CanBePet()
		{
			if (Global.CurrentUnix() > LastPet)
			{
				LastPet = Global.CurrentUnix() + 30;
				return true;
			}
			return false;
		}
	}
}
