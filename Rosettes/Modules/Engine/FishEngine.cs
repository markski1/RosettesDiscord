using Discord.Interactions;
using Discord;

namespace Rosettes.Modules.Engine
{
    public static class FishEngine
    {
        public static string GetFishDBName(int type)
        {
            return type switch
            {
                1 => "fish",
                2 => "uncommonfish",
                3 => "rarefish",
                4 => "shrimp",
                _ => "garbage",
            };
        }

        public static string GetFishName(int type)
        {
            return type switch
            {
                1 => "Common fish",
                2 => "Uncommon fish",
                3 => "Rare fish",
                4 => "Shrimp",
                _ => "Garbage",
            };
        }

        public static string GetFishEmoji(int type)
        {
            return type switch
            {
                1 => "🐡",
                2 => "🐟",
                3 => "🐠",
                4 => "🦐",
                _ => "🗑"
            };
        }

        public static string GetItemName(string choice)
        {
            return choice switch
            {
                "sushi" => "🍣 Sushi",
                "rice" => "🍙 Rice",
                "shrimprice" => "🍚 Shrimp Fried Rice",
                "garbage" => "🗑 Garbage",
                _ => "invalid item"
            };
        }

        public static string GetFullFishName(int type)
        {
            return $"{GetFishEmoji(type)} {GetFishName(type)}";
        }

        public static async void ModifyFish(User dbUser, int type, int amount)
        {
            await UserEngine._interface.ModifyInventoryItem(dbUser, GetFishDBName(type), amount);
        }

        public static async void ModifyItem(User dbUser, string choice, int amount)
        {
            await UserEngine._interface.ModifyInventoryItem(dbUser, choice, amount);
        }

        public static async Task<int> GetFish(User dbUser, int type)
        {
            return await UserEngine._interface.FetchInventoryItem(dbUser, GetFishDBName(type));
        }

        public static async Task<int> GetItem(User dbUser, string name)
        {
            return await UserEngine._interface.FetchInventoryItem(dbUser, name);
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

        internal static async Task<string> HasIngredients(User dbUser, string item)
        {
            string ingredients;
            switch (item)
            {
                case "sushi":
                    ingredients = $"2 {GetFullFishName(1)}, 1 {GetFullFishName(2)} and 1 {GetItemName("rice")}.";
                    if (await GetFish(dbUser, 1) >= 2 && await GetFish(dbUser, 2) >= 1 && await GetItem(dbUser, "rice") >= 1)
                    {
                        return "success";
                    }
                    return ingredients;
                case "shrimprice":
                    ingredients = $"2 {GetFullFishName(4)} and 1 {GetItemName("rice")}";
                    if (await GetFish(dbUser, 1) >= 2 && await GetFish(dbUser, 2) >= 1 && await GetItem(dbUser, "rice") >= 1)
                    {
                        return "success";
                    }
                    return ingredients;
            }
            return "error";
        }
        public static string MakeItem(User dbUser, string item)
        {
            switch (item)
            {
                case "sushi":
                    ModifyFish(dbUser, 1, -2);
                    ModifyFish(dbUser, 2, -1);
                    ModifyItem(dbUser, "rice", -1);
                    ModifyItem(dbUser, "sushi", +1);
                    return $"2 {GetFullFishName(1)}, 1 {GetFullFishName(2)} and 1 {GetItemName("rice")}";
                case "shrimprice":
                    ModifyFish(dbUser, 4, -2);
                    ModifyItem(dbUser, "rice", -1);
                    ModifyItem(dbUser, "shrimprice", +1);
                    return $"2 {GetFullFishName(1)} and 1 {GetFullFishName(2)}";
            }
            return "nothing";
        }

        public static bool IsValidMakeChoice(string choice)
        {
            string[] choices = { "sushi", "shrimprice" };
            return choices.Contains(choice);
        }

        public static bool IsValidGiveChoice(string choice)
        {
            string[] choices = { "sushi", "shrimprice" };
            return choices.Contains(choice);
        }

        public static bool IsValidUseChoice(string choice)
        {
            string[] choices = { "sushi", "shrimprice", "garbage" };
            return choices.Contains(choice);
        }

        internal static async Task<string> ShopBuy(User user, int option, string name)
        {
            switch (option)
            {
                case 1:
                    if (await GetFish(user, 3) >= 1)
                    {
                        ModifyFish(user, 3, -1);
                        ModifyItem(user, "rice", +2);
                        return $"[{name}] You have purchased 2 {GetItemName("rice")} for 1 {GetFullFishName(3)}";
                    }
                    else
                    {
                        return $"[{name}] You don't have enough {GetFullFishName(3)}";
                    }
                case 2:
                    if (await GetItem(user, "garbage") >= 2)
                    {
                        ModifyItem(user, "garbage", -2);
                        ModifyFish(user, 1, +1);
                        return $"[{name}] You have purchased 1 {GetFullFishName(1)} for 2 {GetItemName("garbage")}";
                    }
                    else
                    {
                        return $"[{name}] You don't have enough {GetItemName("garbage")}";
                    }
                case 3:
                    if (await GetItem(user, "garbage") >= 5)
                    {
                        ModifyItem(user, "garbage", -5);
                        ModifyFish(user, 2, +1);
                        return $"[{name}] You have purchased 1 {GetFullFishName(2)} for 5 {GetItemName("garbage")}";
                    }
                    else
                    {
                        return $"[{name}] You don't have enough {GetItemName("garbage")}";
                    }
            }
            return $"[{name}] Invalid option. Must be the number of your selection.";
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
                    (<= 35) => 1,
                    //uncommon fish
                    (> 35 and <= 55) => 2,
                    // rare fish
                    (> 55 and <= 65) => 3,
                    // shrimp
                    (> 65 and < 85) => 4,
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
                catch
                {
                    // sometimes system timer will get all sus and call this last stage twice, I don't know why.
                    // this just avoids crashing if it tries to delete the same message a second time and discord yells at us
                }
            }
        }
    }
}
