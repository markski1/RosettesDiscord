/*
 * Anonimous Telemetry
 * 
 * The purpose of this is to see which commands are actually used.
 * We do NOT store which users use which commands, or in which guilds.
 * 
 * Only count of interactions, commands, messages, all of which are global and not tied to any entity.
 * 
 * Motivation:
 * 
 * - Remove things that have 0 use.
 * - Improve things which get lots of use.
 */

using System.Collections.Concurrent;
using Dapper;
using Newtonsoft.Json;
using Rosettes.Core;
using Rosettes.Database;

namespace Rosettes.Modules.Engine;

public enum TelemetryType {
    Command,
    Interaction,
    Message
}

public static class TelemetryEngine
{
    private static int _commandCount;
    private static int _interactionCount;
    private static int _messageCount;
    private static readonly System.Timers.Timer Timer = new(60 * 60 * 1000);

    private static ConcurrentDictionary<string, int> UseByCommand = new();

    public static void Setup()
    {
        Timer.Elapsed += SyncTelemetry;
        Timer.AutoReset = true;
        Timer.Enabled = true;
    }

    private static async void SyncTelemetry(object? source, System.Timers.ElapsedEventArgs e)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.Db;

        const string sql = """
                           INSERT INTO telemetry (cmd_count, interaction_count, message_count, count_by_command)
                           VALUES(@CommandCount, @InteractionCount, @MessageCount, @UseByCommand)
                           """;

        var snapshotCmd = Interlocked.Exchange(ref _commandCount, 0);
        var snapshotInt = Interlocked.Exchange(ref _interactionCount, 0);
        var snapshotMsg = Interlocked.Exchange(ref _messageCount, 0);
        var snapshotDict = Interlocked.Exchange(ref UseByCommand, new ConcurrentDictionary<string, int>());

        try
        {
            await db.ExecuteAsync(sql, new { CommandCount = snapshotCmd, InteractionCount = snapshotInt, MessageCount = snapshotMsg, UseByCommand = JsonConvert.SerializeObject(snapshotDict) });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("TelemetryEngine", $"Failed to sync telemetry. \n ```{ex}```");
        }
    }

    public static void Count(TelemetryType telemetryType)
    {
        switch (telemetryType)
        {
            case TelemetryType.Command:
                Interlocked.Increment(ref _commandCount);
                return;
            case TelemetryType.Interaction:
                Interlocked.Increment(ref _interactionCount);
                return;
            case TelemetryType.Message:
                Interlocked.Increment(ref _messageCount);
                return;
            default:
                return;
        }
    }

    public static void CountCommand(string commandName)
    {
        UseByCommand.AddOrUpdate(commandName, 1, (_, count) => count + 1);
    }
}
