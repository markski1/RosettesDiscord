namespace Rosettes.Modules.Engine.RPG
{
    public static class Farm
    {

    }

    public class Crop
    {
        public int plotId;
        public ulong userId;
        public uint unixGrowth;
        public uint unixNextWater;
        public int cropType;

        public Crop(int plot_id, ulong user_id, uint unix_growth, uint unix_next_water, int crop_type)
        {
            plotId = plot_id;
            userId = user_id;
            unixGrowth = unix_growth;
            unixNextWater = unix_next_water;
            cropType = crop_type;
        }
    }
}
