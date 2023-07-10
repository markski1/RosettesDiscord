using Discord.WebSocket;
using Discord;
using Rosettes.Core;
using Rosettes.Database;

/*
	The pet system is currently overcomplicated due to a lack of foresight when pets were first added.
	As they were initially just collectibles that were treated as a commodity and couldn't be individually addressed
	or interacted with, they were originally only stored as either a 0 or a 1 in a string field in the database.

	As the pet system got further developed into becoming, well, a system, it became a necessity to append the new functionality
	onto that rather janky foundation.

	I will eventually rewrite this into a less messy state. It won't be hard, but it'll require like an hour or two of tedium.
	For now, it is what it is.
*/

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
			foreach (char aPet in petsOwned)
			{
				if (aPet == '1')
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
			foreach (Pet aPet in petList)
			{
				petMenu.AddOption(label: aPet.GetName(), value: $"{aPet.Index}");
			}

			petMenu.MaxValues = 1;

			comps.WithSelectMenu(petMenu);
			FarmEngine.AddStandardButtons(ref buttonRow, "fish");

			comps.AddRow(buttonRow);

			Pet? pet = await PetEngine.GetUserPet(dbUser);

			if (pet is not null)
			{
				ActionRowBuilder petRow = new();
				petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
				petRow.WithButton(label: $"{pet.GetEmoji()} information", customId: $"pet_view", style: ButtonStyle.Secondary);
				comps.AddRow(petRow);
			}

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

			int happinessGained = receivingPet.DoPet();

			if (happinessGained < 0)
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
			ActionRowBuilder buttonRow = new();
			FarmEngine.AddStandardButtons(ref buttonRow, "shop");
			comps.AddRow(buttonRow);

			Pet? ownPet = await GetUserPet(dbUser);

			if (ownPet is not null)
			{
				petRow.WithButton(label: $"Pet {userGuildRef.DisplayName}'s {ownPet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Secondary);
			}

			if (dbUser != receiverUser) petRow.WithButton(label: $"Pet {receiverGuildUser.DisplayName}'s {receivingPet.GetName()}", customId: $"doPet_{receiverUser.Id}", style: ButtonStyle.Secondary);
			comps.AddRow(petRow);

			embed.Footer = new EmbedFooterBuilder() { Text = $"Pet has gained {happinessGained} happiness." };

			await component.RespondAsync(embed: embed.Build(), components: comps.Build());
		}

		public static async Task<int> RollForPet(User dbUser)
		{
			if (Global.Chance(3))
			{
				int pet;
				int attempts = 0;
				while (true)
				{
					pet = Global.Randomize(23);
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

			await component.RespondAsync(embed: embed.Build(), ephemeral: true);
		}

		public static async Task FeedAPet(SocketMessageComponent component)
		{
			var dbUser = await UserEngine.GetDBUser(component.User);

			EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

			string foodItem = component.Data.Values.Last();

			int petId = int.Parse(component.Data.CustomId[8..]);

			Pet pet;

			try
			{
				pet = PetCache.First(x => x.Id == petId); // first() may throw an exception in some failure cases.
				if (pet == null) { throw new Exception("pet not found"); } // if first() don't throw an exception but we failed all the same, force it.
			}
			catch
			{
				await component.RespondAsync("Sorry, there was an error finding that pet.", ephemeral: true);
				return;
			}

			int foodAvailable = await FarmEngine.GetItem(dbUser, foodItem);

			if (foodAvailable <= 0)
			{
				await component.RespondAsync($"You don't have any {FarmEngine.GetItemName(foodItem)}.", ephemeral: true);
				return;
			}

			int happinessGained = pet.DoFeed(foodItem);

			if (happinessGained < 0) {
				if (happinessGained == -1) await component.RespondAsync("Pets may only be fed fish of any type, shrimps or carrots", ephemeral: true);
				if (happinessGained == -2) await component.RespondAsync("Pets may only be fed once in a 5 minute window.", ephemeral: true);
				return;
			}

			FarmEngine.ModifyItem(dbUser, foodItem, -1);

			embed.Title = $"{pet.GetName()} has been fed.";
			embed.Description = $"Pet has eaten {FarmEngine.GetItemName(foodItem)}. Yum!";

			embed.Footer = new EmbedFooterBuilder() { Text = $"Pet has gained {happinessGained} happiness." };

			ComponentBuilder comps = new();
			ActionRowBuilder buttonRow = new();

			FarmEngine.AddStandardButtons(ref buttonRow, "shop");
			comps.AddRow(buttonRow);

			ActionRowBuilder petRow = new();
			petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
			petRow.WithButton(label: "All pets", customId: "pets", style: ButtonStyle.Secondary);

			comps.AddRow(petRow);

			await component.RespondAsync(embed: embed.Build(), components: comps.Build());
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
			embed.AddField("Happiness", $"{pet.GetHappiness()}%", inline: true);
			embed.AddField("Found", $"<t:{pet.GetFoundDate()}:R>");
			embed.AddField("Experience", $"{pet.GetExp()}xp");

			ComponentBuilder comps = new();
			ActionRowBuilder petRow = new();
			ActionRowBuilder buttonRow = new();
			FarmEngine.AddStandardButtons(ref buttonRow);
			comps.AddRow(buttonRow);

			petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
			petRow.WithButton(label: "Change name", customId: "pet_namechange", style: ButtonStyle.Secondary);
			petRow.WithButton(label: "All pets", customId: "pets", style: ButtonStyle.Secondary);

			SelectMenuBuilder feedMenu = new()
			{
				Placeholder = $"Feed {pet.GetName()}...",
				CustomId = $"petFeed_{pet.Id}",
				MinValues = 1,
				MaxValues = 1
			};

			feedMenu.AddOption(label: FarmEngine.GetItemName("fish"), value: "fish");
			feedMenu.AddOption(label: FarmEngine.GetItemName("uncommonfish"), value: "uncommonfish");
			feedMenu.AddOption(label: FarmEngine.GetItemName("shrimp"), value: "shrimp");
			feedMenu.AddOption(label: FarmEngine.GetItemName("carrot"), value: "carrot");

			feedMenu.MaxValues = 1;

			comps.AddRow(petRow).WithSelectMenu(feedMenu);

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
			ActionRowBuilder buttonRow = new();
			FarmEngine.AddStandardButtons(ref buttonRow);
			comps.AddRow(buttonRow);

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

		public static bool AcceptablePetMeal(string foodItem)
		{
			string[] choices =
				{
					"fish",
					"uncommonfish",
					"rarefish",
					"shrimp",
					"carrot",
				};
			return choices.Contains(foodItem);
		}

		public static void TimedThings()
		{
			int happiness;
			foreach (Pet pet in PetCache)
			{
				happiness = pet.GetHappiness();
				if (happiness > 80)
				{
					pet.ModifyHappiness(-1);
				}
				else if (happiness > 40)
				{
					if (Global.Chance(66)) pet.ModifyHappiness(-1); // 66% chance of losing 1 happiness point
				}
				else
				{
					if (Global.Chance(25)) pet.ModifyHappiness(-1); // 25% chance of losing 1 happiness point
				}
			}
		}
	}
}