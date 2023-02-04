namespace Rosettes.Modules.Engine.RPG
{
    public static class Farm
    {

    }

    public class Crop
    {
        public int plotId;
        public ulong userId;
        public int unixGrowth;
        public int cropType;

        public Crop(int plot_id, ulong user_id, int unix_growth, int crop_type)
        {
            plotId = plot_id;
            userId = user_id;
            unixGrowth = unix_growth;
            cropType = crop_type;
        }
    }
}
