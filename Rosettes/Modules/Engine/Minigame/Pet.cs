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
		private int TimesPet;
		private int Exp;
		private int FoundDate;
		private int Happiness;
		public ulong ownerId;
		private string Name;

		public int LastInteracted = 0;
		public int LastPet = 0;
		public int LastFed = 0;
		public bool SyncUpToDate = false;

		public Pet(int index, ulong owner_id, string name)
		{
			Index = index;
			ownerId = owner_id;
			Name = name;
			FoundDate = Global.CurrentUnix();
			TimesPet = 0;
			Exp = 0;
			Happiness = 100;
		}

		public Pet(int pet_id, int pet_index, ulong owner_id, string pet_name, int exp, int times_pet, int found_date, int happiness)
		{
			Id = pet_id;
			Index = pet_index;
			ownerId = owner_id;
			Name = pet_name;
			TimesPet = times_pet;
			Exp = exp;
			FoundDate = found_date;
			Happiness = happiness;
			// if pet was never initialized, last interaction will be set as now.
			if (FoundDate == 0)
			{
				FoundDate = Global.CurrentUnix();
			}
		}

		public string GetEmoji()
		{
			return PetEngine.PetEmojis(Index);
		}

		// If the animal can be pet, apply the appropiate effects and return true.
		// Otherwise return false.
		public bool DoPet()
		{
			if (Global.CurrentUnix() > LastPet)
			{
				Random Random = new();
				LastPet = Global.CurrentUnix() + 30;
				ModifyHappiness(+(Random.Next(10) + 5)); // add anywhere from 5 to 14% happiness
				AddExp(1);
				TimesPet++;
				SyncUpToDate = false;
				return true;
			}
			return false;
		}

		public int DoFeed(string foodItem)
		{
			if (!PetEngine.AcceptablePetMeal(foodItem))
			{
				return -1; // Error: Pets may only be fed fish of any type, shrimps or carrots
			}

			if (Global.CurrentUnix() > LastFed)
			{
				Random Random = new();
				LastFed = Global.CurrentUnix() + 300;
				int happinessMod = (Random.Next(10) + 5);
				ModifyHappiness(+happinessMod); // add anywhere from 5 to 14% happiness
				AddExp(1);
				TimesPet++;
				SyncUpToDate = false;
				return happinessMod;
			}
			else
			{
				return -2; // Error: Pets may only be fed once in a 5 minute window.
			}
		}

		public void ModifyHappiness(int modify)
		{
			Happiness += modify;
			if (Happiness > 100) Happiness = 100;
			else if (Happiness < 0) Happiness = 0;
		}
		
		// Returns the pet's custom name, or the generic animal name if the pet has not been named.
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

		public void AddExp(int exp)
		{
			Exp += exp;
		}

		public int GetExp()
		{
			return Exp;
		}

		public int GetTimesPet()
		{
			return TimesPet;
		}

		public string GetBareName()
		{
			return Name;
		}

		public void SetName(string newName)
		{
			Name = newName;
			SyncUpToDate = false;
		}

		public int GetFoundDate()
		{
			return FoundDate;
		}

		internal int GetHappiness()
		{
			return Happiness;
		}
	}
}
