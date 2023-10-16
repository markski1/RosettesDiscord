using Discord;
using Discord.Interactions;
using Rosettes.Core;

namespace Rosettes.Modules.Commands.Utility;

public class MusicCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("music", "Music function information")]
    public async Task MusicInfo()
    {
        EmbedBuilder embed = await Global.MakeRosettesEmbed();

        embed.Title = "Rosettes no longer supports music playback";

        embed.Description = "Rosettes' music playback feature was very underused, and at the same time, the largest source of pain as far as maintenance and things breaking go.\n\nFor that reason, I've decided to no longer support the feature. Sorry.";

        await RespondAsync(embed: embed.Build());
    }
}