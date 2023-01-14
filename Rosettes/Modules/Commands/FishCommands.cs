    using Discord;
using Discord.Interactions;
using Rosettes.Core;
using Rosettes.Modules.Engine;
using System.Linq.Expressions;

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
            await RespondAsync($"Fishing! {new Emoji("🎣")}");
            var message = await ReplyAsync("You caught");
            _ = new StartFishing(message, dbUser);
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
                Title = $"{Context.User.Username}'s fish inventory"
            };

            var user = await UserEngine.GetDBUser(Context.User);

            embed.AddField($"{FishHelper.GetFishEmoji(1)}", $"{FishHelper.GetFishName(1)}: {user.FishCount}");
            embed.AddField($"{FishHelper.GetFishEmoji(2)}", $"{FishHelper.GetFishName(2)}: {user.FishCount}");
            embed.AddField($"{FishHelper.GetFishEmoji(999)}", $"{FishHelper.GetFishName(999)}: {user.FishCount}");

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
            var OrderedList = users.OrderByDescending(x => x.FishCount + x.RareFishCount).Take(10);

            string topList = "Top 10 fishers in this list: ```";

            foreach (var user in OrderedList)
            {
                topList += $"{await user.GetName()} | {user.FishCount + user.RareFishCount} fish total\n";
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
                int caught = rand.Next(101);
                caught = caught switch
                {
                    //common fish
                    (<= 40) => 1,
                    //rare fish
                    (> 40 and <= 60) => 2,
                    // garbage
                    _ => 999,
                };
                await message.ModifyAsync(x => x.Content = $"You caught... {FishHelper.GetFishName(caught)}! {FishHelper.GetFishEmoji(caught)}");
                await message.Channel.SendMessageAsync($"*{FishHelper.GetFishName(caught)} {FishHelper.GetFishEmoji(caught)} added to inventory.*");

                _ = caught switch
                {
                    1 => user.FishCount += 1,
                    2 => user.RareFishCount += 1,
                    _ => user.GarbageCount += 1
                };

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
                    // something system timer will get all sus and call this last stage twice, I don't know why.
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
                2 => "Rare fish",
                _ => "Garbage",
            };
        }

        public static Emoji GetFishEmoji(int type)
        {
            return type switch
            {
                1 => new Emoji("🐟"),
                2 => new Emoji("🐠"),
                _ => new Emoji("🗑")
            };
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