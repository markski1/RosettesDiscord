using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.VisualBasic;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using Rosettes.Modules.Engine.Minigame;

namespace Rosettes.Modules.Commands.Minigame
{
	[Group("pet", "Pet system commands")]
	public class PetCommands : InteractionModuleBase<SocketInteractionContext>
	{
		[SlashCommand("view", "View your current pet")]
		public async Task ViewPet()
		{
			var user = await UserEngine.GetDBUser(Context.User);
			Pet? pet = PetEngine.GetUserPet(user);
			if (pet is null)
			{
				await RespondAsync("You don't have a main pet currently set.", ephemeral: true);
				return;
			}

			EmbedBuilder embed = await Global.MakeRosettesEmbed(user);

			embed.Title = "Pet information";
			embed.Description = $"Name: {pet.Name} \n Type: {PetEngine.PetNames(pet.Index)}";

			embed.AddField("Times been pet", $"{pet.timesPet}");
			embed.AddField("Experience", $"{pet.Exp}xp");

			await RespondAsync(embed: embed.Build());
		}

		[SlashCommand("list", "List all your pets")]
		public async Task ListPets()
		{
			await PetEngine.ShowPets(Context.Interaction, Context.User);
		}
	}
}