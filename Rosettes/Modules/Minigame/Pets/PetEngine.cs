using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Minigame.Farming;

namespace Rosettes.Modules.Minigame.Pets;

public static class PetEngine
{
    private static List<Pet> _petCache = [];
    private static readonly Lock PetCacheLock = new();

    private static readonly Dictionary<int, (string fullName, string emoji)> PetChart = new()
    {
        //  db_id   name          emoji
        {   1,    ( "Dog",        "🐕" ) },
        {   2,    ( "Fox",        "🦊" ) },
        {   3,    ( "Cat",        "🐈" ) },
        {   4,    ( "Goat",       "🐐" ) },
        {   5,    ( "Rabbit",     "🐇" ) },
        {   6,    ( "Bat",        "🦇" ) },
        {   7,    ( "Bird",       "🐦" ) },
        {   8,    ( "Lizard",     "🦎" ) },
        {   9,    ( "Hamster",    "🐹" ) },
        {   10,   ( "Frog",       "🐸" ) },
        {   11,   ( "Raccoon",    "🦝" ) },
        {   12,   ( "Panda",      "🐼" ) },
        {   13,   ( "Mouse",      "🐁" ) },
        {   14,   ( "Crocodile",  "🐊" ) },
        {   15,   ( "Turtle",     "🐢" ) },
        {   16,   ( "Otter",      "🦦" ) },
        {   17,   ( "Parrot",     "🦜" ) },
        {   18,   ( "Skunk",      "🦨" ) },
        {   19,   ( "Chipmunk",   "🐿" ) },
        {   20,   ( "Bee",        "🐝" ) },
        {   21,   ( "Owl",        "🦉" ) },
        {   22,   ( "Wolf",       "🐺" ) },
        {   23,   ( "Shark",      "🦈" ) },
        {   24,   ( "Sheep",      "🐑" ) },
        {   25,   ( "Deer",       "🦌" ) },
        {   26,   ( "Butterfly",  "🦋" ) },
        {   27,   ( "Penguin",    "🐧" ) },
        {   28,   ( "Snake",      "🐍" ) },
        {   29,   ( "Duck",       "🦆" ) },
        {   30,   ( "Pig",        "🐖" ) }
    };

    public static string PetNames(int id)
    {
        if (!PetChart.TryGetValue(id, out var value))
            return "? Invalid Pet";

        return $"{value.emoji} {value.fullName}";
    }

    public static string PetEmojis(int id)
    {
        if (!PetChart.TryGetValue(id, out var value))
            return "?";

        return value.emoji;
    }

    public static async Task LoadAllPetsFromDatabase()
    {
        IEnumerable<Pet> petCacheTemp = await PetRepository.GetAllPetsAsync();
        var loaded = petCacheTemp.ToList();
        lock (PetCacheLock)
        {
            _petCache = loaded;
        }
    }

    public static async Task<Pet?> EnsurePetExists(ulong ownerId, int index)
    {
        try
        {
            Pet? pet;
            lock (PetCacheLock)
            {
                pet = _petCache.Find(x => x.OwnerId == ownerId && x.Index == index);
            }

            if (pet is not null) return pet;

            pet = new(index, ownerId, "[not named]");
            pet.Id = await PetRepository.InsertPet(pet);
            lock (PetCacheLock)
            {
                _petCache.Add(pet);
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
        lock (PetCacheLock)
        {
            return _petCache.Find(x => x.OwnerId == user.Id && x.Index == user.MainPet);
        }
    }

    private static List<Pet> GetAllUserPets(User user)
    {
        lock (PetCacheLock)
        {
            return _petCache.FindAll(x => x.OwnerId == user.Id);
        }
    }

    public static async Task ShowPets(SocketInteraction interaction, IUser user)
    {
        User dbUser = await UserEngine.GetDbUser(user);

        await interaction.DeferAsync();

        string petString = "", petString2 = "";

        List<Pet> ownedPets = GetAllUserPets(dbUser);

        bool toggle = true;
        foreach (Pet aPet in ownedPets)
        {
            if (toggle)
                petString += $"{aPet.GetName()}\n";
            else
                petString2 += $"{aPet.GetName()}\n";

            toggle = !toggle;
        }

        if (petString == "")
        {
            petString = "None. You can randomly find pets during activities such as fishing.";
        }

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser);
        Global.AddTitle(container, "### Pets");
        container.WithTextDisplay($"**Pets in ownership:**\n{petString}");

        if (petString2 != "")
            container.WithTextDisplay($"**=====**\n{petString2}");

        SelectMenuBuilder petMenu = new()
        {
            Placeholder = "Set equipped pet",
            CustomId = "defaultPet"
        };
        petMenu.AddOption(label: "None", value: "0");
        foreach (Pet aPet in ownedPets)
        {
            petMenu.AddOption(label: aPet.GetName(), value: $"{aPet.Index}");
        }

        petMenu.MaxValues = 1;

        ActionRowBuilder menuRow = new();
        menuRow.WithSelectMenu(petMenu);
        container.WithActionRow(menuRow);

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, "fish");
        container.WithActionRow(buttonRow);

        Pet? pet = await GetUserPet(dbUser);

        if (pet is not null)
        {
            ActionRowBuilder petRow = new();
            petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
            petRow.WithButton(label: $"{pet.GetEmoji()} information", customId: "pet_view", style: ButtonStyle.Secondary);
            container.WithActionRow(petRow);
        }

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await interaction.FollowupAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    public static async Task PetAPet(SocketMessageComponent component)
    {
        User dbUser = await UserEngine.GetDbUser(component.User);

        string action = component.Data.CustomId;
        ulong id = ulong.Parse(action[6..]);

        User receiverUser = UserEngine.GetCachedDbUserById(id);
        Pet? receivingPet = await GetUserPet(receiverUser);

        if (receivingPet is null)
        {
            await component.RespondAsync("Sorry, there was an error finding that pet.", ephemeral: true);
            return;
        }

        // To get guild display names...
        if (component.User is not SocketGuildUser userGuildRef)
        {
            await component.RespondAsync("Sorry, there was an error finding that pet's owner in the guild.", ephemeral: true);
            return;
        }

        int happinessGained = receivingPet.DoPet();

        if (happinessGained < 0)
        {
            await component.RespondAsync("Sorry, animals can only be pet once every 30 seconds.", ephemeral: true);
            return;
        }

        var receiverGuildUser = userGuildRef.Guild.GetUser(id);

        string description;
        if (receiverUser != dbUser)
            description = $"{userGuildRef.Mention} has pet {receiverGuildUser.Mention}'s pet {receivingPet.GetName()}.";
        else
            description = $"{userGuildRef.Mention} has pet their own {receivingPet.GetName()}.";

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser);
        Global.AddTitle(container, "### *pets!\\*");
        container.WithTextDisplay(description);
        Global.AddFooter(container, $"Pet has gained {happinessGained} happiness.");

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, "shop");

        Pet? ownPet = await GetUserPet(dbUser);

        ActionRowBuilder petRow = new();
        if (ownPet is not null)
            petRow.WithButton(label: $"Pet {userGuildRef.DisplayName}'s {ownPet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Secondary);
        if (dbUser != receiverUser)
            petRow.WithButton(label: $"Pet {receiverGuildUser.DisplayName}'s {receivingPet.GetName()}", customId: $"doPet_{receiverUser.Id}", style: ButtonStyle.Secondary);

        container.WithActionRow(buttonRow);
        container.WithActionRow(petRow);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await component.RespondAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    public static async Task<int> RollForPet(User dbUser)
    {
        if (!Global.Chance(3)) return 0;
        
        int pet;
        int attempts = 0;
        
        while (true)
        {
            pet = Global.Randomize(PetChart.Count) + 1;
            if (!HasPet(dbUser, pet)) break;

            // if after 4 attempts there are only repeated pets, don't get a pet.
            attempts++;
            if (attempts == 4) return 0;
        }
        
        await EnsurePetExists(dbUser.Id, pet);

        return pet;
    }

    public static async Task SetDefaultPet(SocketMessageComponent component)
    {
        var dbUser = await UserEngine.GetDbUser(component.User);

        int petRequested = int.Parse(component.Data.Values.Last());

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser);
        Global.AddTitle(container, "### Pet settings");

        if (petRequested < 1 || petRequested > PetChart.Count)
        {
            dbUser.SetPet(0);
            container.WithTextDisplay("Pet unequipped.");
            container.WithTextDisplay("You no longer have a pet equipped.");
        }
        else if (HasPet(dbUser, petRequested))
        {
            dbUser.SetPet(petRequested);
            container.WithTextDisplay("Pet equipped.");
            container.WithTextDisplay($"Your equipped pet is now your {PetNames(petRequested)}");
        }
        else
        {
            container.WithTextDisplay("Pet not equipped.");
            container.WithTextDisplay($"You do not have a {PetNames(petRequested)}");
        }

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await component.RespondAsync(components: comps.Build(), flags: MessageFlags.Ephemeral | MessageFlags.ComponentsV2);
    }

    public static async Task FeedAPet(SocketMessageComponent component)
    {
        var dbUser = await UserEngine.GetDbUser(component.User);

        string foodItem = component.Data.Values.Last();

        int petId = int.Parse(component.Data.CustomId[8..]);

        Pet? pet;

        lock (PetCacheLock)
        {
            pet = _petCache.Find(x => x.Id == petId);
        }

        if (pet is null)
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

        if (happinessGained < 0)
        {
            switch (happinessGained)
            {
                case -1:
                    await component.RespondAsync("Pets may only be fed fish of any type, shrimps or carrots", ephemeral: true);
                    break;
                case -2:
                    await component.RespondAsync("Pets may only be fed once in a 5 minute window.", ephemeral: true);
                    break;
            }

            return;
        }

        Global.FireAndForget(FarmEngine.ModifyItem(dbUser, foodItem, -1));

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser);
        Global.AddTitle(container, $"### {pet.GetName()} has been fed.");
        container.WithTextDisplay($"Pet has eaten {FarmEngine.GetItemName(foodItem)}. Yum!");
        Global.AddFooter(container, $"Pet has gained {happinessGained} happiness.");

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow, "shop");
        container.WithActionRow(buttonRow);

        ActionRowBuilder petRow = new();
        petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
        petRow.WithButton(label: "All pets", customId: "pets", style: ButtonStyle.Secondary);
        container.WithActionRow(petRow);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await component.RespondAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    private static bool HasPet(User dbUser, int id)
    {
        return _petCache.Any(x => x.OwnerId == dbUser.Id && x.Index == id);
    }

    public static async Task ViewPet(SocketInteraction interaction, IUser user)
    {
        var dbUser = await UserEngine.GetDbUser(user);
        Pet? pet = await GetUserPet(dbUser);
        if (pet is null)
        {
            await interaction.RespondAsync("You don't have a pet equipped.", ephemeral: true);
            return;
        }

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser);
        Global.AddTitle(container, $"**{pet.GetEmoji()} {pet.GetBareName()}**");

        container.WithTextDisplay(
            $"**Type:** {PetNames(pet.Index)}\n" +
            $"**Times pet:** {pet.GetTimesPet()}  |  **Happiness:** {pet.GetHappiness()}%\n" +
            $"**Found:** <t:{pet.GetFoundDate()}:R>  |  **Experience:** {pet.GetExp()}xp"
        );

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

        ActionRowBuilder menuRow = new();
        menuRow.WithSelectMenu(feedMenu);
        container.WithActionRow(menuRow);

        ActionRowBuilder petRow = new();
        petRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
        petRow.WithButton(label: "Change name", customId: "pet_namechange", style: ButtonStyle.Secondary);
        petRow.WithButton(label: "All pets", customId: "pets", style: ButtonStyle.Secondary);
        container.WithActionRow(petRow);

        ActionRowBuilder buttonRow = new();
        FarmEngine.AddStandardButtons(ref buttonRow);
        container.WithActionRow(buttonRow);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await interaction.RespondAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    public static async Task BeginNameChange(SocketMessageComponent component)
    {
        var dbUser = await UserEngine.GetDbUser(component.User);
        Pet? pet = await GetUserPet(dbUser);
        if (pet is null)
        {
            await component.RespondAsync("You don't have a pet equipped.", ephemeral: true);
            return;
        }

        string currentName = pet.GetBareName();

        TextInputBuilder nameInput = new();
        nameInput.WithCustomId("newName");
        nameInput.WithStyle(TextInputStyle.Short);
        nameInput.WithPlaceholder(currentName == "[not named]" ? "Give your pet a name" : currentName);
        nameInput.WithMinLength(3);
        nameInput.WithMaxLength(20);
        nameInput.WithRequired(true);

        LabelBuilder nameLabel = new();
        nameLabel.WithLabel("Rename your pet");
        nameLabel.WithDescription($"Costs 25 {FarmEngine.GetItemName("dabloons")}. 3-20 characters.");
        nameLabel.WithComponent(nameInput);

        ModalBuilder modal = new();
        modal.WithTitle($"{pet.GetEmoji()} {pet.GetName()}");
        modal.WithCustomId("petNamechange");
        modal.AddLabel(nameLabel);

        await component.RespondWithModalAsync(modal.Build());
    }

    public static async Task SetPetName(SocketModal modal, string newName)
    {
        var dbUser = await UserEngine.GetDbUser(modal.User);
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

        Global.FireAndForget(FarmEngine.ModifyItem(dbUser, "dabloons", -25));

        pet.SetName(newName);

        ContainerBuilder container = await Global.MakeRosettesContainer(dbUser);
        Global.AddTitle(container, "**Name changed!**");
        container.WithTextDisplay($"You have changed your pet's name to {pet.GetName()}");
        Global.AddFooter(container, $"Cost: 25 {FarmEngine.GetItemName("dabloons")}");

        ActionRowBuilder buttonRow = new();
        buttonRow.WithButton(label: $"Pet {pet.GetName()}", customId: $"doPet_{dbUser.Id}", style: ButtonStyle.Primary);
        buttonRow.WithButton(label: "View pet", customId: "pet_view", style: ButtonStyle.Secondary);
        container.WithActionRow(buttonRow);

        await Global.AddAuthorFooter(container, dbUser);

        ComponentBuilderV2 comps = new();
        comps.WithContainer(container);

        await modal.RespondAsync(components: comps.Build(), flags: MessageFlags.ComponentsV2);
    }

    public static async void SyncWithDatabase()
    {
        List<Pet> snapshot;
        lock (PetCacheLock)
        {
            snapshot = [.._petCache];
        }

        try
        {
            foreach (var pet in snapshot)
            {
                await pet.UpdateSelf();
            }
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("petengine-save", $"Error syncing pets to db. {ex.Message}");
        }
    }

    public static bool AcceptablePetMeal(string foodItem)
    {
        return FarmEngine.InventoryItems.TryGetValue(foodItem, out var item) && item.can_pet_eat;
    }

    public static void TimedThings()
    {
        List<Pet> snapshot;
        lock (PetCacheLock)
        {
            snapshot = [.._petCache];
        }

        foreach (Pet pet in snapshot)
        {
            var happiness = pet.GetHappiness();

            switch (happiness)
            {
                // Depending on how the happiness ranges, the pet may lose happiness.
                case > 80:
                {
                    if (Global.Chance(80)) pet.ModifyHappiness(-1);
                    break;
                }
                case > 60:
                {
                    if (Global.Chance(40)) pet.ModifyHappiness(-1);
                    break;
                }
                default:
                {
                    if (Global.Chance(15)) pet.ModifyHappiness(-1);
                    break;
                }
            }
        }
    }
}