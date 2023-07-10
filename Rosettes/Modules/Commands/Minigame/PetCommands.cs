using Discord.Interactions;
using Rosettes.Modules.Engine.Minigame;

namespace Rosettes.Modules.Commands.Minigame
{
    [Group("pet", "Pet system commands")]
    public class PetCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("view", "View your current pet")]
        public async Task ViewPet()
        {
            await PetEngine.ViewPet(Context.Interaction, Context.User);
        }

        [SlashCommand("list", "List all your pets")]
        public async Task ListPets()
        {
            await PetEngine.ShowPets(Context.Interaction, Context.User);
        }
    }
}