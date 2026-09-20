using Discord.Interactions;
using Rosettes.Modules.Minigame.Pets;

namespace Rosettes.Modules.Commands.Minigame;

[Group("pet", "Pet system commands")]
public class PetCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("view", "View your current pet")]
    public Task ViewPet() => PetEngine.ViewPet(Context.Interaction, Context.User);

    [SlashCommand("list", "List all your pets")]
    public Task ListPets() => PetEngine.ShowPets(Context.Interaction, Context.User);
}
