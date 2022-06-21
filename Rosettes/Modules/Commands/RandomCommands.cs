using Discord;
using Discord.Commands;
using Rosettes.Core;

namespace Rosettes.Modules.Commands
{
    [Summary("Commands which simply return a random output based on what you provide it.")]
    public class RandomCommands : ModuleBase<SocketCommandContext>
    {
        [Command("dice")]
        [Summary("Generates a number between 1 and the provided number.\nExample usage: '$dice 20' (rolls a d20)")]
        public async Task Dice(int num = -69420)
        {
            if (num == -69420)
            {
                await ReplyAsync($"Usage: `{Settings.Prefix}dice <amount>`");
            }
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
        
        public async Task Ask()
        {
            string[] replies = new string[]
            {
                "yea",
                "no",
                "`(Rosettes is troubled by your question)`",
                "purrhaps",
                "maybe you should CS:GO get some bitches instead",
                "i think so",
                "probably not",
                "there's a strong chance",
                "maybe!",
                "no way",
                "consider morbing",
                $"perhaps you should leave it to a {Settings.Prefix}coin flip"
            };
            await ReplyAsync(replies[Global.Random.Next(replies.Length - 1)]);
        }

        [Command("coin")]
        [Summary("Throw a coin! It'll fall on either face or tails. Alternatively, you can provide two custom faces.\nExample usage: '$coin' or '$coin one two' for custom coin faces named one and two.")]
        public async Task CoinCommand(string face1 = "Tails", string face2 = "Face")
        {
            await Context.Channel.TriggerTypingAsync();
            IUserMessage messageID = await ReplyAsync($"*A coin is thrown into the air... {face1} on one side, {face2} on the other.*");
            // the object takes care of everything, this allows us to just spawn a theoretically infinite amount of coins for any server at once.
            _ = new Coin(messageID, face1, face2);
        }

        [Command("checkem")]
        [Summary("Want to gamble something on dubs, trips, maybe even quads? Check'Em!")]
        public async Task CheckEm()
        {
            Random randomizer = new();
            var File = Directory.GetFiles("./checkem/").OrderBy(x => randomizer.Next()).Take(1);

            int number = randomizer.Next(99999999) + 1;
            if (number < 10000000)
            {
                number += 10000000;
            }

            await ReplyAsync($"[{Context.User.Username}] Check'Em! : **{number}**");

            if (File is null) return;
            await Context.Channel.SendFileAsync(File.First());
        }
    }

    public class Coin
    {
        private readonly System.Timers.Timer Timer = new(2500);
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
            message.ModifyAsync(x => x.Content = $"*The coin lands.* ***{coinSides[Global.Random.Next(2)]}!***");
            Timer.Stop();
            Timer.Dispose();
        }
    }
}