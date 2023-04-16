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

		public Pet(int pet_id, int index, ulong owner_id, string name, int exp, int times_pet)
		{
			Id = pet_id;
			Index = index;
			ownerId = owner_id;
			Name = name;
			timesPet = times_pet;
			Exp = exp;
		}
	}
}
