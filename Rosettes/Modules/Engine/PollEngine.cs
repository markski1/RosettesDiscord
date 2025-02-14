using Dapper;
using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;

namespace Rosettes.Modules.Engine;

public static class PollEngine
{
    public static async Task<bool> AddPoll(ulong id, string question, string option1, string option2, string option3, string option4)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           INSERT INTO polls (id, question, option1, option2, option3, option4)
                           VALUES(@Id, @Question, @Option1, @Option2, @Option3, @Option4)
                           """;

        try
        {
            return await db.ExecuteAsync(
                sql, 
                new { 
                    Id = id, 
                    Question = question,
                    Option1 = option1,
                    Option2 = option2,
                    Option3 = option3,
                    Option4 = option4 
                }) > 0;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-addPoll", $"sqlException code {ex.Message}");
            return false;
        }
    }

    public static async Task<string> VoteInPoll(ulong userId, SocketUserMessage pollMessage, string option)
    {
        if (option.Length > 2) return "There was an error counting your vote.";

        string[] possibleColumns = ["count1", "count2", "count3", "count4"];

        string columnName = $"count{option}";

        if (!possibleColumns.Contains(columnName))
        {
            return "There was an error counting your vote.";
        }

        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        var sql = """
              INSERT INTO poll_votes (user_id, poll_id)
              VALUES(@userId, @Id)
              """;

        try
        {
            await db.ExecuteAsync(sql, new { userId, pollMessage.Id });
        }
        catch
        {
            // failure at this point indicates user already voted
            return "You have already voted in this poll.";
        }

        // this looks ugly, but at the start of the function we ensure 'columnName' can only be a safe value.
        sql = $"UPDATE polls SET `{columnName}` = {columnName}+1 WHERE id=@Id";
        // even if safe, concatenating strings to form SQL queries isn't pretty
        // but I couldn't find any way to provide dynamic column names with Dapper.

        try
        {
            await db.ExecuteAsync(sql, new { pollMessage.Id });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("sql-voteCount", $"sqlException code {ex.Message}");
            // if we fail to count the actual vote, then remove the has voted entry.
            sql = @"DELETE FROM poll_votes WHERE user_id=@userId AND poll_id=@Id)";
            try
            {
                await db.ExecuteAsync(sql, new { userId, pollMessage.Id });
            }
            catch
            {
                // Reaching this block constitutes a catastrophic failure. We managed to log a user as having voted without having counted their vote.
                // This should be impossible. But it would be irresponsible to not at least check and log that we ever get here.
                Global.GenerateErrorMessage("sql-voteCountFailedUncount", $"A user was set as having voted without counting the vote. - sqlException code {ex.Message}");
            }
            return "Sorry, there was an error adding your vote.";
        }

        sql = $"SELECT question, option1, option2, option3, option4, count1, count2, count3, count4 FROM polls WHERE id=@Id";

        Poll? pollResult = await db.QueryFirstOrDefaultAsync<Poll>(sql, new { pollMessage.Id });

        if (pollResult is null) return "Your vote has been counted.";
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

        try
        {
            await pollMessage.ModifyAsync(msg => msg.Components = comps.Build());
        }
        catch
        {
            return "Your vote was counted, but I can't update the poll results because I don't have permissions to this channel. Please tell an admin!";
        }

        return "Your vote has been counted.";
    }
}

public class Poll(
    string question,
    string option1,
    string option2,
    string option3,
    string option4,
    uint count1,
    uint count2,
    uint count3,
    uint count4)
{
    public string Question = question;
    public string Option1 = option1;
    public string Option2 = option2;
    public string Option3 = option3;
    public string Option4 = option4;
    public uint Count1 = count1;
    public uint Count2 = count2;
    public uint Count3 = count3;
    public uint Count4 = count4;
}
