using Discord;
using Discord.WebSocket;
using System.Text;

namespace Rosettes.Core
{
    public static class Global
    {
        public static readonly RosettesMain RosettesMain = new();
        public static readonly HttpClient HttpClient = new();

        public static void GenerateErrorMessage(string source, string error)
        {
            // generate the error string
            string _error = $"There was an error at \"{source}\".\n```{error}```\n";

            // send it to error channel
            var client = ServiceManager.GetService<DiscordSocketClient>();
            if (client.GetChannel(984608927775854594) is not ITextChannel errorChannel) return;

            if (_error.Length > 1999)
            {
                _error = _error[..1900];
                _error += "```(truncated)";
            }

            errorChannel.SendMessageAsync(_error);

            // and log it to a file
            _error = $"{DateTime.UtcNow} | There was an error at \"{source}\".\n{error}\n\n";
            try
            {
                using var fileStream = new FileStream("./errors.log", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                fileStream.Write(Encoding.UTF8.GetBytes($"{_error}\n"));
            }
            catch
            {
                // meh, just don't stop
            }
        }

        public static void GenerateNotification(string message)
        {
            // send it to #impawtant-data
            var client = ServiceManager.GetService<DiscordSocketClient>();
            if (client.GetChannel(984608927775854594) is not ITextChannel impawtantChannel) return;

            impawtantChannel.SendMessageAsync(message);
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