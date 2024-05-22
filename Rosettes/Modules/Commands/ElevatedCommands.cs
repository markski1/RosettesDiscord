﻿using Dapper;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Database;
using Rosettes.Managers;
using System.Diagnostics;

namespace Rosettes.Modules.Commands;

public class ElevatedCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("about", "About rosettes.")]
    public async Task GetMemory()
    {
        using Process proc = Process.GetCurrentProcess();
        TimeSpan elapsed = DateTime.Now - proc.StartTime;
        string runtimeText = "";
        if (elapsed.Days > 0)
        {
            runtimeText += $"{elapsed.Days} day{((elapsed.Days != 1) ? 's' : null)}, ";
        }
        if (elapsed.Hours > 0)
        {
            runtimeText += $"{elapsed.Hours} hour{((elapsed.Hours != 1) ? 's' : null)}, ";
        }
        runtimeText += $"{elapsed.Minutes} minute{((elapsed.Minutes != 1) ? 's' : null)} ago.";

        var client = ServiceManager.GetService<DiscordSocketClient>();

        EmbedBuilder embed = await Global.MakeRosettesEmbed();
        embed.Title = "About Rosettes.";
        embed.Description = "A simple, free, open source discord bot.";
        embed.ThumbnailUrl = "https://markski.ar/images/rosettes.png";

        embed.AddField("Memory in use", $"{(ulong)(proc.PrivateMemorySize64 / 1024):N0} Kb", inline: true);
        embed.AddField("Threads", $"{proc.Threads.Count}", inline: true);
        embed.AddField("Currently serving", $"{client.Guilds.Sum(x => x.MemberCount)} users, across {client.Guilds.Count} guilds.");
        embed.AddField("Uptime", runtimeText, inline: true);
        embed.AddField("Ping to Discord", $"{client.Latency}ms", inline: true);

        embed.AddField("Learn about me", "<https://markski.ar/rosettes>");

        EmbedFooterBuilder footer = new() { Text = "Good morning, Dave." };

        embed.Footer = footer;

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("devcmd", "Developer command.")]
    public async Task AdminMenu(string function)
    {
        if (!Global.CheckSnep(Context.User.Id))
        {
            await RespondAsync("This command is snep exclusive.");
            return;
        }
        if (function is "halt" or "restart")
        {
            await RespondAsync("Syncing cache data with database...");

            if (function is "restart")
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "Rosettes is restarting...");
                if (await RosettesMain.HaltOrRestart(true))
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "Rosettes has restarted.\n\nGood morning, Dave.");
                }
                else
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "Rosettes failed to restart.");
                }
            }
            else
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "Rosettes is shutting down.");
                await RosettesMain.HaltOrRestart();
            }
        }
    }

    [SlashCommand("keygen", "Generates a unique key for logging into the Rosettes admin panel.")]
    public async Task KeyGen()
    {
        if (Context.Guild is not null)
        {
            await RespondAsync("Please use this command in my DM's, not here.", ephemeral: true);
            return;
        }

        using var getConn = DatabasePool.GetConnection();
        var db = getConn.db;

        var sql = @"SELECT count(1) FROM login_keys WHERE id=@Id";

        bool hasKey;

        try
        {
            hasKey = await db.ExecuteScalarAsync<bool>(sql, new { Context.User.Id });
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("keygen-getcode", $"sqlException code {ex.Message}");
            return;
        }

        if (hasKey)
        {
            sql = @"UPDATE login_keys SET login_key=@NewKey WHERE id=@Id";
        }
        else
        {
            sql = @"INSERT INTO login_keys (id, login_key)
                        VALUES(@Id, @NewKey)";
        }

        int randNumba;
        string NewKey = "";
        char Character;
        int offset;
        for (int i = 0; i < 24; i++)
        {
            if (Global.Randomize(2) == 0)
            {
                offset = 65;
            }
            else
            {
                offset = 97;
            }
            randNumba = Global.Randomize(0, 26);
            Character = Convert.ToChar(randNumba + offset);
            NewKey += Character;
        }

        try
        {
            await db.ExecuteAsync(sql, new { Context.User.Id, NewKey });
        }
        catch (Exception ex)
        {
            await RespondAsync("Sorry, there was an error generating a Rosettes key for you. Please try again in a while.");
            Global.GenerateErrorMessage("keygen", $"Error! {ex.Message}");
            return;
        }

        if (hasKey)
        {
            await RespondAsync($"You have renewed your Rosettes key.");
        }
        else
        {
            await RespondAsync($"You have been issued a Rosettes key. This is an unique, private identifier to be used in Rosettes-related services. You can change it at any time by using `/keygen` again.");
        }

        await ReplyAsync($"```{NewKey}```");
    }
}