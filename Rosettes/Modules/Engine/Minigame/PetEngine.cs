using Discord.WebSocket;
using Discord;
using Rosettes.Core;
using Rosettes.Database;
using Microsoft.VisualBasic;
using System.ComponentModel;

namespace Rosettes.Modules.Engine.Minigame
{
	public static class PetEngine
	{
		private static List<Pet> PetCache = new();
		public static readonly PetRepository _interface = new();

		public static async void LoadAllPetsFromDatabase()
		{
			IEnumerable<Pet> petCacheTemp;
			petCacheTemp = await _interface.GetAllPetsAsync();
			PetCache = petCacheTemp.ToList();
		}

		public static async Task<Pet?> EnsurePetExists(ulong owner_id, int index)
		{
			Pet? pet;

			try
			{
				pet = PetCache.Find(x => x.ownerId == owner_id && x.Index == index);
				if (pet is null)
				{
					pet = new(index, owner_id, "[not named]");
					PetCache.Add(pet);
					pet.Id = await _interface.InsertPet(pet);
				}
				return pet;
			}
			catch
			{
				return null;
			}
		}

		public static async Task<Pet?> GetUserPet(User user)
		{
			if (user.MainPet <= 0) return null;
			await EnsurePetExists(user.Id, user.MainPet);
			try
			{
				return PetCache.Find(x => x.ownerId == user.Id && x.Index == user.MainPet);
			}
			catch
			{
				return null;
			}
		}

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

			await interaction.DeferAsync();

			embed.Title = $"Pets";

			string petString = "", petString2 = "";
			List<Pet> petList = new();

			string petsOwned = await FarmEngine.GetStrItem(dbUser, "pets");

			int count = 1;

			bool toggle = true;
			foreach (char pet in petsOwned)
			{
				if (pet == '1')
				{
					Pet? getPet = await EnsurePetExists(dbUser.Id, count);
					if (getPet is not null)
					{
						petList.Add(getPet);

						if (toggle) 
							petString += $"{getPet.GetName()}\n";
						else
							petString2 += $"{getPet.GetName()}\n";

						toggle = !toggle;
					}
				}
				count++;
			}

			if (petString == "")
			{
				petString = "None. You can randomly find pets during activities such as fishing.";
			}

			embed.AddField("Pets in ownership:", petString, inline: true);

			if (petString2 != "")
			{
				embed.AddField("=====", petString2, inline: true);
			}

			embed.Description = null;

			ComponentBuilder comps = new();

			ActionRowBuilder buttonRow = new();

			SelectMenuBuilder petMenu = new()
			{
				Placeholder = "Set equipped pet",
				CustomId = "defaultPet"
			};
			petMenu.AddOption(label: "None", value: "0");
			foreach (Pet pet in petList)
			{
				petMenu.AddOption(label: pet.GetName(), value: $"{pet.Index}");
			}

			petMenu.MaxValues = 1;

			comps.WithSelectMenu(petMenu);
			FarmEngine.AddStandardButtons(ref buttonRow);

			comps.AddRow(buttonRow);

			await interaction.FollowupAsync(embed: embed.Build(), components: comps.Build());
		}

		public static async Task PetAPet(SocketMessageComponent component)
		{
			User dbUser = await UserEngine.GetDBUser(component.User);
			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

			embed.Title = $"*pets!\\*";

			string action = component.Data.CustomId;
			ulong id = ulong.Parse(action[6..]);

			User receiverUser = UserEngine.GetDBUserById(id);
			Pet? receivingPet = await GetUserPet(receiverUser);

			if (receivingPet is null)
			{
				await component.RespondAsync("Sorry, there was an error finding that pet!", ephemeral: true);
				return;
			}

			// In order to get guild display names...
			if (component.User is not SocketGuildUser userGuildRef)
			{
				await component.RespondAsync("Sorry, there was an error finding that pet's owner in the guild!", ephemeral: true);
				return;
			}

			if (!receivingPet.CanBePet())
			{
				await component.RespondAsync("Sorry, animals can only be pet once every 30 seconds", ephemeral: true);
				return;
			}

			var receiverGuildUser = userGuildRef.Guild.GetUser(id);

			if (receiverUser != dbUser)
			{
				embed.Description = $"{userGuildRef.Mention} has pet {receiverGuildUser.Mention}'s pet {receivingPet.GetName()}.";
			}
			else
			{
				embed.Description = $"{userGuildRef.Mention} has pet their own {receivingPet.GetName()}.";
			}

			ComponentBuilder comps = new();
			ActionRowBuilder petRow = new();

			Pet? ownPet = await GetUserPet(dbUser);

			if (ownPet is not null)
			{
				petRow.WithButton(label: $"Pet {userGuildRef.DisplayName}'s {ownPet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Secondary);
				if (dbUser != receiverUser) petRow.WithButton(label: $"Pet {receiverGuildUser.DisplayName}'s {receivingPet.GetName()}", customId: $"doPet_{receiverUser.Id}", style: ButtonStyle.Secondary);
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
				embed.Title = "Pet unequipped.";
				embed.Description = "You no longer have a pet equipped.";
			}
			else if (await HasPet(dbUser, petRequested))
			{
				dbUser.SetPet(petRequested);
				embed.Title = "Pet equipped.";
				embed.Description = $"Your equipped pet is now your {PetEngine.PetNames(petRequested)}";
			}
			else
			{
				embed.Title = "Pet not equipped.";
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

		public static async Task ViewPet(SocketInteraction interaction, IUser user)
		{
			var dbUser = await UserEngine.GetDBUser(user);
			Pet? pet = await GetUserPet(dbUser);
			if (pet is null)
			{
				await interaction.RespondAsync("You don't have a pet equipped.", ephemeral: true);
				return;
			}

			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

			embed.Title = "Pet information";
			embed.Description = $"**Name:** {pet.GetBareName()} \n **Type:** {PetEngine.PetNames(pet.Index)}";

			embed.AddField("Times been pet", $"{pet.GetTimesPet()}", inline: true);
			embed.AddField("Happiness", $"100%", inline: true);
			embed.AddField("Experience", $"{pet.GetExp()}xp");

			ComponentBuilder comps = new();
			ActionRowBuilder petRow = new();

			petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
			petRow.WithButton(label: "Change name", customId: "pet_namechange", style: ButtonStyle.Secondary);

			comps.AddRow(petRow);

			await interaction.RespondAsync(embed: embed.Build(), components: comps.Build());
		}

		public static async void BeginNameChange(SocketMessageComponent component)
		{
			var dbUser = await UserEngine.GetDBUser(component.User);
			Pet? pet = await GetUserPet(dbUser);
			if (pet is null)
			{
				await component.RespondAsync("You don't have a pet equipped.", ephemeral: true);
				return;
			}

			ModalBuilder modal = new()
			{
				Title = $"Change the name of \"{pet.GetName()}\"",
				CustomId = "petNamechange"
			};

			modal.AddTextInput($"Enter the new name.", "newName", placeholder: $"It will have a cost of 25 {FarmEngine.GetItemName("dabloons")}", minLength: 5, maxLength: 25);

			await component.RespondWithModalAsync(modal.Build());
		}

		public static async void SetPetName(SocketModal modal, string newName)
		{
			var dbUser = await UserEngine.GetDBUser(modal.User);
			Pet? pet = await GetUserPet(dbUser);
			if (pet is null)
			{
				await modal.RespondAsync("You don't have a pet equipped.", ephemeral: true);
				return;
			}

			if (await FarmEngine.GetItem(dbUser, "dabloons") < 25)
			{
				await modal.RespondAsync($"You don't have 25 {FarmEngine.GetItemName("dabloons")} to change your pet's name.", ephemeral: true);
				return;
			}

			FarmEngine.ModifyItem(dbUser, "dabloons", -25);

			pet.SetName(newName);

			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

			embed.Title = "Name changed!";
			embed.Description = $"You have changed your pet's name to {pet.GetName()}";
			

			ComponentBuilder comps = new();
			ActionRowBuilder petRow = new();

			petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
			petRow.WithButton(label: $"View pet", customId: $"pet_view", style: ButtonStyle.Secondary);

			comps.AddRow(petRow);

			embed.Footer = new EmbedFooterBuilder() { Text = $"Cost: 25 {FarmEngine.GetItemName("dabloons")}" };

			await modal.RespondAsync(embed: embed.Build(), components: comps.Build());
		}

		public static async void SyncWithDatabase()
		{
			foreach (Pet pet in PetCache)
			{
				if (pet.SyncUpToDate) continue;
				await _interface.UpdatePet(pet);
				pet.SyncUpToDate = true;
			}
		}
	}
}
