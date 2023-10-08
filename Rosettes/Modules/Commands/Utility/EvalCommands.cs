using Discord.Interactions;
using Rosettes.Core;
using Discord;

namespace Rosettes.Modules.Commands.Utility
{
    public class EvalCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("eval", "Evaluates an expression.")]
        public async Task Profile(string expression)
        {
            if (expression.Contains('^'))
            {
                await RespondAsync("Looks like you tried to use ^. For powers, please use `Pow(num1, num2)` instead.", ephemeral: true);
                return;
            }
            double result;
            try
            {
                var eval = new NCalc.Expression(expression);
                Func<double> f = eval.ToLambda<double>();
                result = f();
            }
            catch (Exception ex)
            {
                await RespondAsync($"Sorry, I could not evaluate your expression. ```{ex.Message}```", ephemeral: true);
                return;
            }

            EmbedBuilder embed = await Global.MakeRosettesEmbed();

            embed.Title = "Expression evaluation.";

            embed.Description = $"The expression `{expression}` evaluates to `{result}`.";

            await RespondAsync(embed: embed.Build());
        }
    }
}