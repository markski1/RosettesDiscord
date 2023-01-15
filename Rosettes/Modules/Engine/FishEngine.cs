using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rosettes.Modules.Engine
{
    public static class FishEngine
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

        public static string GetFishDBName(int type)
        {
            return type switch
            {
                1 => "fish",
                2 => "uncommonfish",
                3 => "rarefish",
                _ => "garbage",
            };
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
            switch (item)
            {
                case "sushi":
                    string ingredients = $"2 {GetFullFishName(1)} and 1 {GetFullFishName(2)}";
                    if (await GetFish(dbUser, 1) >= 2 && await GetFish(dbUser, 2) >= 1)
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
                    ModifyItem(dbUser, "sushi", +1);
                    return $"2 {GetFullFishName(1)} and 1 {GetFullFishName(2)}";
            }
            return "nothing";
        }

        public static bool IsValidMakeChoice(string choice)
        {
            string[] choices = { "sushi" };
            return choices.Contains(choice);
        }

        public static string GetItemName(string choice)
        {
            return choice switch
            {
                "sushi" => $"{new Emoji("🍣")} Sushi",
                "garbage" => $"{new Emoji("🗑")} Garbage",
                _ => "invalid item"
            };
        }

        public static bool IsValidGiveChoice(string choice)
        {
            string[] choices = { "sushi" };
            return choices.Contains(choice);
        }

        public static bool IsValidUseChoice(string choice)
        {
            string[] choices = { "sushi", "garbage" };
            return choices.Contains(choice);
        }
    }
}
