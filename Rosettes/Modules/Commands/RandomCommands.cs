using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
    public class RandomCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("dice", "Returns a random number between 1 and the provided number.")]
        public async Task Dice(int num = -999)
        {
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (dbGuild is not null)
            {
                if (!dbGuild.AllowsRandom())
                {
                    await RespondAsync("Sorry, but the guild admins have disabled the use of this type of commands.");
                    return;
                }
            }
            if (num == -999)
            {
                await RespondAsync($"Usage: `/dice <amount>`");
            }
            if (num < 1)
            {
                await RespondAsync("The number cannot be lower than 1.");
            }
            else if (num > 1000000)
            {
                await RespondAsync("The number cannot be greater than 1 million.");
            }
            else
            {
                Random Random = new();
                await RespondAsync((Random.Next(num)+1).ToString());
            }
        }

        [SlashCommand("ask", "Ask the virtual snep a question.")]
        
        public async Task Ask()
        {
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (dbGuild is not null)
            {
                if (!dbGuild.AllowsRandom())
                {
                    await RespondAsync("Sorry, but the guild admins have disabled the use of this type of commands.");
                    return;
                }
            }
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
                $"perhaps you should leave it to a /coin flip"
            };
            Random Random = new();
            await RespondAsync(replies[Random.Next(replies.Length - 1)]);
        }

        [SlashCommand("coin", "Throw a coin! It'll fall on either face or tails. Alternatively, you can provide two custom faces.")]
        public async Task CoinCommand(string face1 = "Tails", string face2 = "Face")
        {
            var dbGuild = await GuildEngine.GetDBGuild(Context.Guild);
            if (dbGuild is not null)
            {
                if (!dbGuild.AllowsRandom())
                {
                    await RespondAsync("Sorry, but the guild admins have disabled the use of this type of commands.");
                    return;
                }
            }
            await Context.Channel.TriggerTypingAsync();
            IUserMessage messageID = await ReplyAsync($"*A coin is thrown into the air... {face1} on one side, {face2} on the other.*");
            // the object takes care of everything, this allows us to just spawn a theoretically infinite amount of coins for any server at once.
            _ = new Coin(messageID, face1, face2);
        }

        [SlashCommand("checkem", "Want to gamble something on dubs, trips, maybe even quads? Check'Em!")]
        public async Task CheckEm()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command cannot be used in DM's. Instead use https://snep.markski.ar/checkem");
                return;
            }

            Random randomizer = new();
            var File = Directory.GetFiles("/var/www/html/checkem/").OrderBy(x => randomizer.Next()).Take(1);

            int number = randomizer.Next(99999999) + 1;
            // kind of a hacky way to ensure the number is 8 digits long. This is just a memey random number thing so it doesn't matter.
            if (number < 10000000)
            {
                number += 10000000;
            }

            string displayName;
            SocketGuildUser? GuildUser = Context.User as SocketGuildUser;
            if (GuildUser is not null && GuildUser.Nickname is not null)
            {
                displayName = GuildUser.Nickname;
            }
            else
            {
                displayName = Context.User.Username;
            }

            await RespondAsync($"[{displayName}] Check'Em! : **{number}**");

            await RespondAsync(File.First().Replace("/var/www/html/", "https://snep.markski.ar/"));
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
            Random Random = new();
            message.ModifyAsync(x => x.Content = $"*The coin lands.* ***{coinSides[Random.Next(2)]}!***");
            Timer.Stop();
            Timer.Dispose();
        }
    }
}