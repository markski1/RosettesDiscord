using Discord.Interactions;
using Discord;
using System.Xml.Linq;

namespace Rosettes.Modules.Engine
{
    public static class RpgEngine
    {
        public static string GetItemName(string choice)
        {
            return choice switch
            {
                "fish" => "🐡 Common fish",
                "uncommonfish" => "🐟 Uncommon fish",
                "rarefish" => "🐠 Rare fish",
                "shrimp" => "🦐 Shrimp",
                "sushi" => "🍣 Sushi",
                "rice" => "🍙 Rice",
                "shrimprice" => "🍚 Shrimp Fried Rice",
                "garbage" => "🗑 Garbage",
                "dabloons" => "🐾 Dabloons",
                _ => "invalid item"
            };
        }

        public static async void ModifyItem(User dbUser, string choice, int amount)
        {
            await UserEngine._interface.ModifyInventoryItem(dbUser, choice, amount);
        }

        public static async Task<int> GetItem(User dbUser, string name)
        {
            return await UserEngine._interface.FetchInventoryItem(dbUser, name);
        }

        internal static async Task<bool> CanuseRPGCommand(SocketInteractionContext context)
        {
            if (context.Guild is null)
            {
                return false;
            }
            var dbGuild = await GuildEngine.GetDBGuild(context.Guild);
            if (!dbGuild.AllowsRPG())
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
                    ingredients = $"2 {GetItemName("fish")}, 1 {GetItemName("uncommonfish")} and 1 {GetItemName("rice")}.";
                    if (await GetItem(dbUser, "fish") >= 2 && await GetItem(dbUser, "uncommonfish") >= 1 && await GetItem(dbUser, "rice") >= 1)
                    {
                        return "success";
                    }
                    return ingredients;
                case "shrimprice":
                    ingredients = $"2 {GetItemName("shrimp")} and 1 {GetItemName("rice")}";
                    if (await GetItem(dbUser, "shrimp") >= 2 && await GetItem(dbUser, "rice") >= 1)
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
                    ModifyItem(dbUser, "fish", -2);
                    ModifyItem(dbUser, "uncommonfish", -1);
                    ModifyItem(dbUser, "rice", -1);
                    ModifyItem(dbUser, "sushi", +1);
                    return $"2 {GetItemName("fish")}, 1 {GetItemName("uncommonfish")} and 1 {GetItemName("rice")}";
                case "shrimprice":
                    ModifyItem(dbUser, "shrimp", -2);
                    ModifyItem(dbUser, "rice", -1);
                    ModifyItem(dbUser, "shrimprice", +1);
                    return $"2 {GetItemName("shrimp")} and 1 {GetItemName("rice")}";
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
                    if (await GetItem(user, "dabloons") >= 5)
                    {
                        ModifyItem(user, "dabloons", -5);
                        ModifyItem(user, "rice", +2);
                        return $"[{name}] You have purchased 2 {GetItemName("rice")} for 5 {GetItemName("dabloons")}";
                    }
                    else
                    {
                        return $"[{name}] You don't have enough {GetItemName("dabloons")}";
                    }
                case 2:
                    if (await GetItem(user, "dabloons") >= 2)
                    {
                        ModifyItem(user, "dabloons", -2);
                        ModifyItem(user, "fish", +1);
                        return $"[{name}] You have purchased 1 {GetItemName("fish")} for 2 {GetItemName("dabloons")}";
                    }
                    else
                    {
                        return $"[{name}] You don't have enough {GetItemName("dabloons")}";
                    }
                case 3:
                    if (await GetItem(user, "dabloons") >= 5)
                    {
                        ModifyItem(user, "dabloons", -5);
                        ModifyItem(user, "uncommonfish", +1);
                        return $"[{name}] You have purchased 1 {GetItemName("uncommonfish")} for 5 {GetItemName("dabloons")}";
                    }
                    else
                    {
                        return $"[{name}] You don't have enough {GetItemName("dabloons")}";
                    }
            }
            return $"[{name}] Invalid buy option. Must be the number of your selection.";
        }

        public static async Task<string> ShopSell(User user, int option, string name)
        {
            switch (option)
            {
                case 1:
                    if (await GetItem(user, "rarefish") >= 1)
                    {
                        ModifyItem(user, "rarefish", -1);
                        ModifyItem(user, "dabloons", +5);
                        return $"[{name}] You have sold 1 {GetItemName("rarefish")} for 5 {GetItemName("dabloons")}";
                    }
                    else
                    {
                        return $"[{name}] You don't have enough {GetItemName("rarefish")}";
                    }
                case 2:
                    if (await GetItem(user, "garbage") >= 5)
                    {
                        ModifyItem(user, "garbage", -5);
                        ModifyItem(user, "dabloons", +5);
                        return $"[{name}] You have sold 5 {GetItemName("garbage")} for 5 {GetItemName("dabloons")}";
                    }
                    else
                    {
                        return $"[{name}] You don't have enough {GetItemName("garbage")}";
                    }
            }
            return $"[{name}] Invalid sell option. Must be the number of your selection.";
        }
    }

    public class StartFishing
    {
        private readonly System.Timers.Timer Timer = new(1000);
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
            await message.ModifyAsync(x => x.Content = "You caught... ");
            await Task.Delay(1000);
            Random rand = new();
            int caught = rand.Next(100);
            string fishingCatch = caught switch
            {
                (<= 35) => "fish",
                (> 35 and <= 55) => "uncommonfish",
                (> 55 and <= 65) => "rarefish",
                (> 65 and < 85) => "shrimp",
                _ => "garbage"
            };
            await message.ModifyAsync(x => x.Content = $"You caught... {RpgEngine.GetItemName(fishingCatch)}!");
            await message.Channel.SendMessageAsync($"*{RpgEngine.GetItemName(fishingCatch)} caught and added to inventory.*");
            RpgEngine.ModifyItem(user, fishingCatch, +1);
            await Task.Delay(1000);
            await message.DeleteAsync();
        }
    }
}
