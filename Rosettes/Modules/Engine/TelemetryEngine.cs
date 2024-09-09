﻿/*
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
 * - Find out: Is Rosettes even worth keeping online?
 */

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
    private static int CommandCount = 0;
    private static int InteractionCount = 0;
    private static int MessageCount = 0;
    private static readonly System.Timers.Timer Timer = new(60 * 60 * 1000);

    private static Dictionary<string, int> UseByCommand = [];

    public static void Setup()
    {
        Timer.Elapsed += SyncTelemetry;
        Timer.AutoReset = true;
        Timer.Enabled = true;
    }

    private static async void SyncTelemetry(Object? source, System.Timers.ElapsedEventArgs e)
    {
        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"INSERT INTO telemetry (cmd_count, interaction_count, message_count, count_by_command)
                        VALUES(@CommandCount, @InteractionCount, @MessageCount, @UseByCommand)"
        ;

        try
        {
            await db.ExecuteAsync(sql, new { CommandCount, InteractionCount, MessageCount, UseByCommand = JsonConvert.SerializeObject(UseByCommand) });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("TelemetryEngine", $"Failed to sync telemetry. \n ```{ex}```");
        }

        UseByCommand.Clear();
        CommandCount = 0;
        InteractionCount = 0;
        MessageCount = 0;
    }

    public static void Count(TelemetryType telemetryType)
    {
        switch (telemetryType)
        {
            case TelemetryType.Command:
                CommandCount++;
                break;
            case TelemetryType.Interaction:
                InteractionCount++;
                break;
            case TelemetryType.Message:
                MessageCount++;
                break;
        }
    }

    public static void CountCommand(string commandName)
    {
        if (UseByCommand.ContainsKey(commandName))
        {
            UseByCommand[commandName]++;
        }
        else
        {
            UseByCommand.Add(commandName, 1);
        }
    }
}