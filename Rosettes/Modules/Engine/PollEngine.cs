using Discord;
using Discord.WebSocket;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Database;
using Dapper;
using Rosettes.Modules.Commands.Alarms;

namespace Rosettes.Modules.Engine
{
    public static class PollEngine
    {
        public static async Task<bool> AddPoll(ulong Id, string Question, string Option1, string Option2, string Option3, string Option4)
        {
            var conn = new MySqlConnection(Settings.Database.ConnectionString);

            var sql = @"INSERT INTO polls (id, question, option1, option2, option3, option4)
                        VALUES(@Id, @Question, @Option1, @Option2, @Option3, @Option4)";

            try
            {
                return (await conn.ExecuteAsync(sql, new { Id, Question, Option1, Option2, Option3, Option4 })) > 0;
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-addPoll", $"sqlException code {ex.Message}");
                return false;
            }
        }

        public static async Task<string> VoteInPoll(ulong userId, SocketUserMessage pollMessage, string Option)
        {
            if (Option.Length > 2) return "There was an error counting your vote.";

            var conn = new MySqlConnection(Settings.Database.ConnectionString);

            // begin by checking if the user has already voted in this poll.
            var sql = @"SELECT user_id FROM poll_votes WHERE user_id=@userId";

            sql = @"INSERT INTO poll_votes (user_id, poll_id)
                    VALUES(@userId, @Id)";

            try
            {
                await conn.ExecuteAsync(sql, new { userId, pollMessage.Id });
            }
            catch
            {
                // failure at this point indicates user already voted
                return "You have already voted in this poll.";
            }

            sql = $"UPDATE polls SET `count{Option}`=count{Option}+1 WHERE id=@Id";

            try
            {
                await conn.ExecuteAsync(sql, new { pollMessage.Id });
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("sql-voteCount", $"sqlException code {ex.Message}");
                // if we fail to count the actual vote, then remove the has voted entry.
                sql = @"DELETE FROM poll_votes WHERE user_id=@userId AND poll_id=@Id)";
                try
                {
                    await conn.ExecuteAsync(sql, new { userId, pollMessage.Id });
                }
                catch
                {
                    // catastrophic db failure --- if we SOMEHOW get here, we managed to log a user as having voted without having counted their vote.
                    // This should NEVER happen, but it would be irresponsible to not at least check if it ever does and see if a more elegant solution is needed.
                    Global.GenerateErrorMessage("sql-voteCountFailedUncount", $"Catastrophic failure, we set a user as having voted without counting the vote. - sqlException code {ex.Message}");
                }
                return "Sorry, there was an error adding your vote.";
            }

            sql = $"SELECT question, option1, option2, option3, option4, count1, count2, count3, count4 FROM polls WHERE id=@Id";

            Poll pollResult = await conn.QueryFirstOrDefaultAsync<Poll>(sql, new { pollMessage.Id });

            if (pollResult is not null)
            {
                var comps = new ComponentBuilder();

                comps.WithButton(label: $"{pollResult.Option1} - {pollResult.Count1} votes", customId: "1", row: 0);
                comps.WithButton(label: $"{pollResult.Option2} - {pollResult.Count2} votes", customId: "2", row: 1);

                if (pollResult.Option3 != "NOT_PROVIDED")
                {
                    comps.WithButton(label: $"{pollResult.Option3} - {pollResult.Count3} votes", customId: "3", row: 2);
                }
                if (pollResult.Option4 != "NOT_PROVIDED")
                {
                    comps.WithButton(label: $"{pollResult.Option4} - {pollResult.Count4} votes", customId: "4", row: 3);
                }

                await pollMessage.ModifyAsync(msg => msg.Components = comps.Build());
            }

            return "Your vote has been counted.";
        }
    }

    public class Poll
    {
        public string Question;
        public string Option1;
        public string Option2;
        public string Option3;
        public string Option4;
        public uint Count1;
        public uint Count2;
        public uint Count3;
        public uint Count4;

        public Poll(string question, string option1, string option2, string option3, string option4, uint count1, uint count2, uint count3, uint count4) {
            Question = question;
            Option1 = option1;
            Option2 = option2;
            Option3 = option3;
            Option4 = option4;
            Count1 = count1;
            Count2 = count2;
            Count3 = count3;
            Count4 = count4;
        }
    }
}
