using Discord;
using Discord.WebSocket;
using MySqlConnector;
using System.Text;

namespace Rosettes.core
{
    public static class Global
    {
        public readonly static Random Random = new();
        public static readonly RosettesMain RosettesMain = new();
        public static readonly HttpClient HttpClient = new();

        public static async void GenerateErrorMessage(string source, string error)
        {
            // generate the error string
            string _error = $"{DateTime.Now.Kind:es-AR} | mew wew! There was an error at \"{source}\".\n\n```{error}```\n\n";

            // log it to a file
            using var fileStream = new FileStream("./errors.log", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            fileStream.Write(Encoding.UTF8.GetBytes($"{_error}\n"));

            // and send it to me on discord
            var client = ServiceManager.GetService<DiscordSocketClient>();
            var me = await client.GetUserAsync(93115098461110272);

            if (_error.Length > 1999)
            {
                _error = _error[..1900];
                _error += "```(truncated)";
            }

            _ = me.SendMessageAsync(_error);
        }

        public static decimal Truncate(decimal d, byte decimals)
        {
            decimal r = Math.Round(d, decimals);

            if (d > 0 && r > d)
            {
                return r - new decimal(1, 0, 0, false, decimals);
            }
            else if (d < 0 && r < d)
            {
                return r + new decimal(1, 0, 0, false, decimals);
            }

            return r;
        }

        public static int CurrentUnix()
        {
            return (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public static bool CheckSnep(ulong id)
        {
            if (id == 93115098461110272)
            {
                return true;
            }
            return false;
        }
    }
}