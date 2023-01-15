using Discord;
using Discord.Interactions;
using Rosettes.Core;
using Rosettes.Modules.Engine;

namespace Rosettes.Modules.Commands
{
    public class FishCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("fish", "Try to catch a fish")]
        public async Task CatchFish()
        {
            if (!await FishEngine.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }
            var dbUser = await UserEngine.GetDBUser(Context.User);
            if (!dbUser.CanFish())
            {
                await RespondAsync("You can only fish every 60 minutes.");
                return;
            }
            await RespondAsync($"[{Context.User.Username}] Fishing! {new Emoji("🎣")}");
            var message = await ReplyAsync("You caught");
            _ = new StartFishing(message, dbUser);
        }

        [SlashCommand("fish-make", "Use your fish to make something.")]
        public async Task FishMake(string option = "none")
        {
            if (!await FishEngine.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }

            string choice = option.ToLower();

            if (FishEngine.IsValidMakeChoice(choice))
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                string ingredientsOrSuccess = await FishEngine.HasIngredients(dbUser, choice);
                if (ingredientsOrSuccess != "success")
                {
                    await RespondAsync($"You need at least {ingredientsOrSuccess} to make {FishEngine.GetItemName(choice)}.", ephemeral: true);
                    return;
                }

                ingredientsOrSuccess = FishEngine.MakeItem(dbUser, choice);

                await RespondAsync($"[{Context.User.Username}] You have spent {ingredientsOrSuccess} to make: {FishEngine.GetItemName(choice)}");
                await ReplyAsync($"1 {FishEngine.GetItemName(choice)} added to inventory.");
            }
            else
            {
                await RespondAsync("Valid things to make: Sushi", ephemeral: true);
                return;
            }
        }

        [SlashCommand("fish-give", "Give an item to another user.")]
        public async Task FishGive(IUser user, string option = "none")
        {
            if (!await FishEngine.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }

            string choice = option.ToLower();

            if (FishEngine.IsValidGiveChoice(choice))
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                var receiver = await UserEngine.GetDBUser(user);

                if (await FishEngine.GetItem(dbUser, choice) < 1)
                {
                    await RespondAsync($"You don't have any {FishEngine.GetItemName(choice)} to give.");
                    return;
                }

                FishEngine.ModifyItem(receiver, choice, +1);

                FishEngine.ModifyItem(dbUser, choice, -1);

                await RespondAsync($"[{Context.User.Username}] have given {FishEngine.GetItemName(choice)} to {user.Mention}!");
            }
            else
            {
                await RespondAsync("Valid things to give: Sushi", ephemeral: true);
                return;
            }
        }

        [SlashCommand("fish-use", "Use an item, optionally with another user.")]
        public async Task FishUse(string option = "none", IUser? user = null)
        {
            if (!await FishEngine.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }

            var dbUser = await UserEngine.GetDBUser(Context.User);

            string choice = option.ToLower();

            if (choice == "sushi")
            {
                if (await FishEngine.GetItem(dbUser, choice) < 1)
                {
                    await RespondAsync($"You don't have any {FishEngine.GetItemName(choice)} to use.");
                    return;
                }

                FishEngine.ModifyItem(dbUser, choice, -1);

                if (user is null)
                {
                    await RespondAsync($"[{Context.User.Username}] has eaten {FishEngine.GetItemName(choice)}. Tasty!");
                }
                else
                {
                    await RespondAsync($"[{Context.User.Username}] has eaten {FishEngine.GetItemName(choice)}, and shared some with {user.Mention}. Tasty!");
                }
            }
            if (choice == "garbage")
            {
                if (await FishEngine.GetItem(dbUser, "garbage") < 1)
                {
                    await RespondAsync($"You don't have any {FishEngine.GetItemName(choice)} to use.");
                    return;
                }

                if (user is null)
                {
                    await RespondAsync($"'Using' garbage requires tagging another member to throw the trash at.", ephemeral: true);
                }
                else
                {
                    FishEngine.ModifyFish(dbUser, 999, -1);
                    await RespondAsync($"[{Context.User.Username}] has thrown some {FishEngine.GetItemName("garbage")} at {user.Mention}. Well done!");
                }
            }
            else
            {
                await RespondAsync("Valid things to use: Sushi, Garbage", ephemeral: true);
                return;
            }
        }

        [SlashCommand("fish-inventory", "Check your fish inventory")]
        public async Task FishInventory()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("Fish commands don't work in DM's.");
                return;
            }
            EmbedBuilder embed = new()
            {
                Title = $"{Context.User.Username}'s inventory"
            };

            var user = await UserEngine.GetDBUser(Context.User);

            embed.AddField(
                $"Fish", 
                $"{FishEngine.GetFullFishName(1)}: {await FishEngine.GetFish(user, 1)} \n" +
                $"{FishEngine.GetFullFishName(2)}: {await FishEngine.GetFish(user, 2)} \n" +
                $"{FishEngine.GetFullFishName(3)}: {await FishEngine.GetFish(user, 3)} \n");
            embed.AddField(
                $"Items",
                $"{FishEngine.GetItemName("garbage")}: {await FishEngine.GetItem(user, "garbage")}\n" +
                $"{FishEngine.GetItemName("sushi")}: {await FishEngine.GetItem(user, "sushi")}");

            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("fish-top", "List the top fishers by total fish in their inventory")]
        public async Task FishTops()
        {
            await RespondAsync("Top fisher leaderboard is currently unavailable!");
            /*
            if (Context.Guild is null)
            {
                await RespondAsync("Fish commands don't work in DM's.");
                return;
            }
            var users = await UserEngine.GetAllUsersFromGuild(Context.Guild);

            if (users is null)
            {
                await RespondAsync("There was an error listing the top users in this guild, sorry.");
                return;
            }

            // Compiler isn't happy about writing on top of the same lists so we create two new ones. It's fiiiine.
            var OrderedList = users.OrderByDescending(x => await FishEngine.GetFish(x, 1) + x.GetFish(2)).Take(10);

            string topList = "Top 10 fishers in this list: ```";

            foreach (var user in OrderedList)
            {
                topList += $"{await user.GetName()} | {user.GetFish(1) + user.GetFish(2)} fish total\n";
            }

            topList += "```";

            await RespondAsync(topList);
            */
        }
    }

    public class StartFishing
    {
        private readonly System.Timers.Timer Timer = new(1000);
        private int fishState = 0;
        private readonly IUserMessage message;
        private readonly User user;

        public StartFishing(IUserMessage _message, User _user)
        {
            message = _message;
            user = _user;
            Timer.Elapsed += FishStateUpdate;
            Timer.AutoReset = false;
            Timer.Enabled = true;
        }

        public async void FishStateUpdate(Object? source, System.Timers.ElapsedEventArgs e)
        {
            if (fishState == 0)
            {
                await message.ModifyAsync(x => x.Content = "You caught... ");
                fishState = 1;
                Timer.Enabled = true;
            }
            if (fishState == 1)
            {
                Random rand = new();
                int caught = rand.Next(100);
                caught = caught switch
                {
                    //common fish
                    (<= 40) => 1,
                    //uncommon fish
                    (> 40 and <= 70) => 2,
                    // rare fish
                    (> 70 and <= 85) => 3,
                    // garbage
                    _ => 999,
                };

                await message.ModifyAsync(x => x.Content = $"You caught... {FishEngine.GetFullFishName(caught)}!");
                await message.Channel.SendMessageAsync($"*{FishEngine.GetFullFishName(caught)} caught and added to inventory.*");

                FishEngine.ModifyFish(user, caught, +1);

                fishState = 2;
                Timer.Enabled = true;
                Timer.Dispose();
            }
            if (fishState == 2)
            {
                try
                {
                    fishState = 3;
                    await message.DeleteAsync();
                }
                catch {
                    // sometimes system timer will get all sus and call this last stage twice, I don't know why.
                    // this just avoids crashing if it tries to delete the same message a second time and discord yells at us
                }
            }
        }
    }
}