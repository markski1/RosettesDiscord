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
            if (!await FishHelper.CanFish(Context))
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
            if (!await FishHelper.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }

            if (option.ToLower() == "sushi")
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                if (dbUser.GetFish(1) < 2 || dbUser.GetFish(2) < 1)
                {
                    await RespondAsync($"You need at least 2 {FishHelper.GetFullFishName(1)} and 1 {FishHelper.GetFullFishName(2)} to make sushi.", ephemeral: true);
                    return;
                }

                dbUser.MakeSushi();

                await RespondAsync($"[{Context.User.Username}] You have spent 2 {FishHelper.GetFishEmoji(1)} and 1 {FishHelper.GetFishEmoji(2)} to make: {FishHelper.WriteSushi()}");
                await ReplyAsync($"1 {FishHelper.WriteSushi()} added to inventory.");
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
            if (!await FishHelper.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }

            if (option.ToLower() == "sushi")
            {
                var dbUser = await UserEngine.GetDBUser(Context.User);
                var receiver = await UserEngine.GetDBUser(user);

                if (dbUser.GetSushi() < 1)
                {
                    await RespondAsync("You don't have any sushi to give.");
                    return;
                }

                dbUser.GiveSushi(receiver);

                await RespondAsync($"[{Context.User.Username}] have given {FishHelper.WriteSushi()} to {user.Mention}!");
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
            if (!await FishHelper.CanFish(Context))
            {
                await RespondAsync("Sorry, fishing commands are disabled in this server.", ephemeral: true);
                return;
            }

            var dbUser = await UserEngine.GetDBUser(Context.User);

            if (option.ToLower() == "sushi")
            {
                if (dbUser.GetSushi() < 1)
                {
                    await RespondAsync("You don't have any sushi to give.");
                    return;
                }

                dbUser.UseSushi();

                if (user is null)
                {
                    await RespondAsync($"[{Context.User.Username}] has eaten {FishHelper.WriteSushi()}. Tasty!");
                }
                else
                {
                    await RespondAsync($"[{Context.User.Username}] has eaten {FishHelper.WriteSushi()}, and shared some with {user.Mention}. Tasty!");
                }
            }
            if (option.ToLower() == "garbage")
            {
                if (dbUser.GetFish(999) < 1)
                {
                    await RespondAsync("You don't have any garbage to throw.");
                    return;
                }

                

                if (user is null)
                {
                    await RespondAsync($"'Using' garbage requires tagging another member to throw the trash at.", ephemeral: true);
                }
                else
                {
                    dbUser.TakeFish(999, 1);
                    await RespondAsync($"[{Context.User.Username}] has thrown some {FishHelper.GetFullFishName(999)} at {user.Mention}. Well done!");
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
                $"{FishHelper.GetFullFishName(1)}: {user.GetFish(1)} \n" +
                $"{FishHelper.GetFullFishName(2)}: {user.GetFish(2)} \n" +
                $"{FishHelper.GetFullFishName(3)}: {user.GetFish(3)} \n");
            embed.AddField(
                $"Items",
                $"{FishHelper.GetFullFishName(999)}: {user.GetFish(999)}\n" +
                $"{FishHelper.WriteSushi()}: {user.GetSushi()}");

            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("fish-top", "List the top fishers by total fish in their inventory")]
        public async Task FishTops()
        {
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
            var OrderedList = users.OrderByDescending(x => x.GetFish(1) + x.GetFish(2)).Take(10);

            string topList = "Top 10 fishers in this list: ```";

            foreach (var user in OrderedList)
            {
                topList += $"{await user.GetName()} | {user.GetFish(1) + user.GetFish(2)} fish total\n";
            }

            topList += "```";

            await RespondAsync(topList);
        }
    }

    public class StartFishing : IDisposable
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

                await message.ModifyAsync(x => x.Content = $"You caught... {FishHelper.GetFullFishName(caught)}!");
                await message.Channel.SendMessageAsync($"*{FishHelper.GetFullFishName(caught)} caught and added to inventory.*");

                user.AddFish(caught, 1);

                fishState = 2;
                Timer.Enabled = true;
            }
            if (fishState == 2)
            {
                try
                {
                    fishState = 3;
                    await message.DeleteAsync();
                    Timer.Enabled = false;
                    Timer.Dispose();
                    Dispose();
                }
                catch {
                    // sometimes system timer will get all sus and call this last stage twice, I don't know why.
                    // this just avoids crashing if it tries to delete the same message a second time and discord yells at us
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public static class FishHelper
    {
        public static string GetFishName(int type)
        {
            return type switch
            {
                1 => "Common fish",
                2 => "Uncommon fish",
                3 => "Rare fish",
                _ => "Garbage",
            };
        }

        public static Emoji GetFishEmoji(int type)
        {
            return type switch
            {
                1 => new Emoji("🐡"),
                2 => new Emoji("🐟"),
                3 => new Emoji("🐠"),
                _ => new Emoji("🗑")
            };
        }

        public static string GetFullFishName(int type)
        {
            return $"{GetFishEmoji(type)} {GetFishName(type)}";
        }

        public static string WriteSushi()
        {
            return $"{new Emoji("🍣")} Sushi";
        }

        internal static async Task<bool> CanFish(SocketInteractionContext context)
        {
            if (context.Guild is null)
            {
                return false;
            }
            var dbGuild = await GuildEngine.GetDBGuild(context.Guild);
            if (!dbGuild.AllowsFishing())
            {
                return false;
            }
            return true;
        }
    }

}