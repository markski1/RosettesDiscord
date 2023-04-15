using Discord.WebSocket;
using Discord;
using Rosettes.Core;
using Rosettes.Modules.Engine.Farming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rosettes.Modules.Engine.Minigame
{
	public static class PetEngine
	{
		public static string PetNames(int id)
		{
			return id switch
			{
				1 => "🐕 Dog",
				2 => "🦊 Fox",
				3 => "🐈 Cat",
				4 => "🐐 Goat",
				5 => "🐇 Rabbit",
				6 => "🦇 Bat",
				7 => "🐦 Bird",
				8 => "🦎 Lizard",
				9 => "🐹 Hamster",
				10 => "🐸 Frog",
				11 => "🦝 Raccoon",
				12 => "🐼 Panda",
				13 => "🐁 Mouse",
				14 => "🐊 Crocodile",
				15 => "🐢 Turtle",
				16 => "🦦 Otter",
				17 => "🦜 Parrot",
				18 => "🦨 Skunk",
				19 => "🐿 Chipmunk",
				20 => "🐝 Bee",
				21 => "🦉 Owl",
				22 => "🐺 Wolf",
				23 => "🦈 Shark",
				_ => "? Invalid Pet"
			};
		}

		public static string PetEmojis(int id)
		{
			return id switch
			{
				1 => "🐕",
				2 => "🦊",
				3 => "🐈",
				4 => "🐐",
				5 => "🐇",
				6 => "🦇",
				7 => "🐦",
				8 => "🦎",
				9 => "🐹",
				10 => "🐸",
				11 => "🦝",
				12 => "🐼",
				13 => "🐁",
				14 => "🐊",
				15 => "🐢",
				16 => "🦦",
				17 => "🦜",
				18 => "🦨",
				19 => "🐿",
				20 => "🐝",
				21 => "🦉",
				22 => "🐺",
				23 => "🦈",
				_ => "?"
			};
		}

		public static async Task ShowPets(SocketInteraction interaction, IUser user)
		{
			User dbUser = await UserEngine.GetDBUser(user);
			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

			embed.Title = $"Pets";

			string petString = "";
			List<int> petList = new();

			string petsOwned = await FarmEngine.GetStrItem(dbUser, "pets");

			int count = 1;

			foreach (char pet in petsOwned)
			{
				if (pet == '1')
				{
					petString += $"{PetEngine.PetNames(count)}\n";
					petList.Add(count);
				}
				count++;
			}

			if (petString == "")
			{
				petString = "None. You can randomly find pets during activities such as fishing.";
			}

			embed.AddField("Pets in ownership:", petString);

			embed.Description = null;

			ComponentBuilder comps = new();

			ActionRowBuilder buttonRow = new();

			SelectMenuBuilder petMenu = new()
			{
				Placeholder = "Set default pet",
				CustomId = "defaultPet"
			};
			petMenu.AddOption(label: "None", value: "0");
			foreach (int pet in petList)
			{
				petMenu.AddOption(label: PetEngine.PetNames(pet), value: $"{pet}");
			}

			petMenu.MaxValues = 1;

			comps.WithSelectMenu(petMenu);
			FarmEngine.AddStandardButtons(ref buttonRow);

			comps.AddRow(buttonRow);

			await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
		}

		public static async Task PetAPet(SocketMessageComponent component)
		{
			User dbUser = await UserEngine.GetDBUser(component.User);
			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

			embed.Title = $"*pets!\\*";

			string action = component.Data.CustomId;

			ulong id = ulong.Parse(action[4..]);

			User receiverUser;

			// In order to get guild display names...
			SocketGuildUser userGuildRef;
			SocketGuildUser receiverGuildRef;

			try
			{
				receiverUser = UserEngine.GetDBUserById(id);
				userGuildRef = component.User as SocketGuildUser;
				receiverGuildRef = userGuildRef.Guild.GetUser(id);
				if (userGuildRef is null || receiverGuildRef is null) throw new Exception("failed to get user references");
				if (receiverUser.MainPet <= 0) throw new Exception("pet not set");
			}
			catch
			{
				await component.RespondAsync("Sorry, there was an error doing that.", ephemeral: false);
				return;
			}

			if (receiverUser != dbUser)
			{
				embed.Description = $"{userGuildRef.Mention} has pet {receiverGuildRef.Mention}'s pet {PetEngine.PetNames(receiverUser.MainPet)}.";
			}
			else
			{
				embed.Description = $"{userGuildRef.Mention} has pet their own pet {PetEngine.PetNames(receiverUser.MainPet)}.";
			}


			ComponentBuilder comps = new();

			ActionRowBuilder petRow = new();



			if (dbUser.MainPet > 0)
			{
				petRow.WithButton(label: $"Pet {userGuildRef.DisplayName}'s {PetEngine.PetNames(dbUser.MainPet)}", customId: $"pet_{dbUser.Id}", style: ButtonStyle.Secondary);
				if (dbUser != receiverUser) petRow.WithButton(label: $"Pet {receiverGuildRef.DisplayName}'s {PetEngine.PetNames(receiverUser.MainPet)}", customId: $"pet_{receiverUser.Id}", style: ButtonStyle.Secondary);
				comps.AddRow(petRow);
			}

			await component.RespondAsync(embed: embed.Build(), components: comps.Build());
		}

		public static async Task<int> RollForPet(User dbUser)
		{
			Random rand = new();

			if (rand.Next(33) == 0)
			{
				int pet;
				int attempts = 0;
				while (true)
				{
					pet = rand.Next(23);
					if (await HasPet(dbUser, pet + 1) == false) break;

					// if after 5 attempts there's only repeated pets, don't get a pet.
					attempts++;
					if (attempts == 5) return 0;
				}

				string userPets = await FarmEngine.GetStrItem(dbUser, "pets");

				char[] petsAsChars = userPets.ToCharArray();

				petsAsChars[pet] = '1';

				FarmEngine.ModifyStrItem(dbUser, "pets", new string(petsAsChars));

				return pet + 1;
			}

			return 0;
		}

		public static async Task SetDefaultPet(SocketMessageComponent component)
		{
			var dbUser = await UserEngine.GetDBUser(component.User);

			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

			int petRequested = int.Parse(component.Data.Values.Last());

			if (petRequested < 1 || petRequested > 23)
			{
				dbUser.SetPet(0);
				embed.Title = "Main pet removed.";
				embed.Description = "You no longer have a main pet.";
			}
			else if (await HasPet(dbUser, petRequested))
			{
				dbUser.SetPet(petRequested);
				embed.Title = "Main pet set.";
				embed.Description = $"Your main pet is now your {PetEngine.PetNames(petRequested)}";
			}
			else
			{
				embed.Title = "Main pet not set.";
				embed.Description = $"You do not have a {PetEngine.PetNames(petRequested)}";
			}

			try
			{
				await component.RespondAsync(embed: embed.Build(), ephemeral: true);
			}
			catch
			{
				await component.RespondAsync(embed: embed.Build(), ephemeral: true);
			}
		}

		public static async Task<bool> HasPet(User dbUser, int id)
		{
			// make zero-indexed
			id--;
			string pets = await FarmEngine.GetStrItem(dbUser, "pets");

			return pets != null && pets[id] == '1';
		}
	}
}
