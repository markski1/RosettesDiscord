using Discord;
using Discord.Commands;
using Rosettes.core;

namespace Rosettes.modules.commands
{
    [Summary("Commands which simply return a random output based on what you provide it.")]
    public class RandomCommands : ModuleBase<SocketCommandContext>
    {
        [Command("dice")]
        [Summary("Generates a number between 1 and the provided number.\nExample usage: '$dice 20' (rolls a d20)")]
        public async Task DiceAsync(int num)
        {
            if (num < 1)
            {
                await ReplyAsync("The number cannot be lower than 1.");
            }
            else if (num > 1000000)
            {
                await ReplyAsync("The number cannot be greater than 1 million.");
            }
            else
            {
                await ReplyAsync((Global.Random.Next(num)+1).ToString());
            }
        }

        [Command("ask")]
        [Summary("Ask the virtual snep a question.\nExample usage: '$ask Am I sus?'")]
        
        public async Task AskAsync()
        {
            string[] replies = new string[]
            {
                "nyea",
                "nyo",
                "mew wew... `(the virtual snep is troubled by your question)`",
                "purrhaps",
                "maybe you should CS:GO get some bitches instead",
                "i think so",
                "probably not",
                "there's a strong chance",
                "maybe!",
                "no way"
            };
            await ReplyAsync(replies[Global.Random.Next(replies.Length - 1)]);
        }

        [Command("coin")]
        [Summary("Throw a coin! It'll fall on either face or tails. Alternatively, you can provide two custom faces.\nExample usage: '$coin' or '$coin one two' for custom coin faces named one and two.")]
        public async Task CoinAsync(string face1 = "Tails", string face2 = "Face")
        {
            await Context.Channel.TriggerTypingAsync();
            IUserMessage messageID = await ReplyAsync($"*The virtual snep throws a coin in the air... {face1} on one side, {face2} on the other.*");
            // the object takes care of everything, this allows us to just spawn a theoretically infinite amount of coins for any server at once.
            _ = new Coin(messageID, face1, face2);
        }
    }

    public class Coin
    {
        private readonly System.Timers.Timer Timer = new(3000);
        private bool coinLanded = false;
        private readonly IUserMessage message;
        private readonly string[] coinSides = new String[2];

        public Coin(IUserMessage OriginalMessage, string face1, string face2)
        {
            coinSides[0] = face1;
            coinSides[1] = face2;
            message = OriginalMessage;
            Timer.Elapsed += CoinStateUpdate;
            Timer.Enabled = true;
        }

        public void CoinStateUpdate(Object? source, System.Timers.ElapsedEventArgs e)
        {
            if (!coinLanded)
            {
                message.ModifyAsync(x => x.Content = "*The coin lands, and begins tumbling about...*");
                coinLanded = true;
                Timer.Enabled = true;
            } else
            {
                message.ModifyAsync(x => x.Content = $"*The coin settles.* ***{coinSides[Global.Random.Next(2)]}!***");
                Timer.Dispose();
            }
        }
    }
}