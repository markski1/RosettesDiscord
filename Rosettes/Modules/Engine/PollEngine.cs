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
        if (!int.TryParse(option, out var optionNumber) || optionNumber is < 1 or > 4)
        {
            return "There was an error counting your vote.";
        }

        string columnName = $"count{optionNumber}";

        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;
        
        const string checkVoteSql = "SELECT 1 FROM poll_votes WHERE user_id = @userId AND poll_id = @pollId";
        if (await db.ExecuteScalarAsync<bool>(checkVoteSql, new { userId, pollId = pollMessage.Id }))
        {
            return "You have already voted in this poll.";
        }

        // Use a transaction so the user vote record and the vote itself are counted at once.
        // Either both succeed or both fail.
        if (db.State == System.Data.ConnectionState.Closed)
        {
            db.Open();
        }

        await using var transaction = await db.BeginTransactionAsync();
        
        try
        {
            const string insertVoteSql = "INSERT INTO poll_votes (user_id, poll_id) VALUES(@userId, @pollId)";
            await db.ExecuteAsync(insertVoteSql, new { userId, pollId = pollMessage.Id }, transaction);

            // Looks dirty, but columnName can't possibly ever be an unsafe value.
            var updateCountSql = $"UPDATE polls SET `{columnName}` = `{columnName}` + 1 WHERE id = @pollId";
            await db.ExecuteAsync(updateCountSql, new { pollId = pollMessage.Id }, transaction);

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Global.GenerateErrorMessage("sql-voteInPoll", $"Transaction failed: {ex.Message}");
            return "Sorry, there was an error adding your vote.";
        }
        
        // Fetch the updated poll
        var sql = $"SELECT question, option1, option2, option3, option4, count1, count2, count3, count4 FROM polls WHERE id=@Id";
        Poll? pollResult = await db.QueryFirstOrDefaultAsync<Poll>(sql, new { pollMessage.Id });

        if (pollResult is null) return "Your vote has been counted, but the results cannot be updated at this time.";
        
        var comps = new ComponentBuilder()
            .WithButton(label: $"{pollResult.Option1} - {pollResult.Count1} votes", customId: "1", row: 0)
            .WithButton(label: $"{pollResult.Option2} - {pollResult.Count2} votes", customId: "2", row: 1);
        
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

public record Poll(
    string Question,
    string Option1,
    string Option2,
    string Option3,
    string Option4,
    uint Count1,
    uint Count2,
    uint Count3,
    uint Count4
);
