using Rosettes.Core;

namespace Rosettes.Modules.Engine.RPG
{
    public static class Farm
    {
        public static async Task<Crop?> InsertCropsInPlot(User dbUser, int cropType, int plot_id)
        {
            Random rand = new();
            int growTime = Global.CurrentUnix() + (3600 * 3) + (3600 * rand.Next(7));
            int waterTime = Global.CurrentUnix() + 1800 + (1800 * rand.Next(4));

            Crop newCrop = new(plot_id, dbUser.Id, growTime, waterTime, cropType);

            bool success = await RpgEngine._interface.InsertCrop(newCrop);

            if (success)
            {
                return newCrop;
            }
            else
            {
                return null;
            }
        }

        public static string GetHarvest(int id)
        {
            return id switch
            {
                1 => "tomato",
                2 => "carrot",
                3 => "potato",
                _ => "invalid crop"
            };
        }
    }

    public class Crop
    {
        public int plotId;
        public ulong userId;
        public int unixGrowth;
        public int unixNextWater;
        public int cropType;

        public Crop(int plot_id, ulong user_id, int unix_growth, int unix_next_water, int crop_type)
        {
            plotId = plot_id;
            userId = user_id;
            unixGrowth = unix_growth;
            unixNextWater = unix_next_water;
            cropType = crop_type;
        }
    }
}
