using Discord;
using Discord.WebSocket;
using Rosettes.Core;
using Rosettes.Database;

namespace Rosettes.Modules.Engine;


/*
 * TODO:
 *
 * - Rate limit auth requests.
 * - Keep a log of requests made from the moment they are made.
 * - Keep that log updated with the user's action.
 * - Consider thiner grained auths and actions to be specified by API users.
 */

public static class AuthEngine
{
    public static async Task<bool> RequestApplicationAuth(ApplicationAuth application, User user)
    {
        EmbedBuilder embed = await Global.MakeRosettesEmbed();
        embed.Title = "Authorization Request";
        embed.Description = "An application is requesting you authorize an action. " +
                            "Please read carefully. Ignore or deny this request if you don't know what it is.";
        
        embed.AddField("Application", application.Name);

        ComponentBuilder comps = new ComponentBuilder();
        
        comps.WithButton("Accept", $"auth_accept:{application.Id}", ButtonStyle.Success);
        comps.WithButton("Deny", $"auth_deny:{application.Id}", ButtonStyle.Danger);

        var userRef = await UserEngine.GetUserReferenceById(user.Id);
        if (userRef is null) return false;

        try
        {
            await userRef.SendMessageAsync(embed: embed.Build(), components: comps.Build());
            return true;
        }
        catch
        {
            // Most likely due to missing perms. User does not allow DMs or is no longer in a server w/ us.
            return false;
        }
    }

    public static async Task<bool> SendApplicationNotification(string applicationName, string message, User user)
    {
        EmbedBuilder embed = await Global.MakeRosettesEmbed();
        embed.Title = "Application Message";
        embed.Description = "An application has issued you the following notification.";
        
        embed.AddField("Application", applicationName);
        embed.AddField("Message", message);
        
        var userRef = await UserEngine.GetUserReferenceById(user.Id);
        if (userRef is null) return false;
        
        try
        {
            await userRef.SendMessageAsync(embed: embed.Build());
            return true;
        }
        catch
        {
            // Most likely due to missing perms. User does not allow DMs or is no longer in a server w/ us.
            return false;
        }
    }

    public static async Task HandleAuthInteraction(SocketMessageComponent component)
    {
        User dbUser = await UserEngine.GetDbUser(component.User);
        EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);

        string action;
        int appId;

        try
        {
            action = component.Data.CustomId.Split(":")[0];
            appId = int.Parse(component.Data.CustomId.Split(":")[1]);
        }
        catch (Exception e)
        {
            await component.RespondAsync("Sorry, there was an error authorizing this. It has been reported.", ephemeral: true);
            Global.GenerateErrorMessage("auth-interaction", $"Error handling auth interaction: {e.Message}");
            return;
        }

        if (action == "auth_accept")
        {
            bool success = await AuthRepository.AuthUser(appId, dbUser.Id);
            
            if (success)
            {
                embed.Title = "Request authorized";
                embed.Description = "You have authorized this application's request. You may now return to the application and follow instructions.";
            }
            else
            {
                embed.Title = "Request denied.";
                embed.Description = "You have denied this application's request. No further action is required.";
            }
        }
        
        await component.RespondAsync(embed: embed.Build());
    }
}

public class ApplicationAuth(int id, string name, ulong ownerId)
{
    public int Id { get; init; } = id;
    public string Name { get; init; } = name;
    public ulong OwnerId { get; init; } = ownerId;
}

public class ApplicationRelation(int applicationId, ulong userId)
{
    public int ApplicationId { get; init; } = applicationId;
    public ulong UserId { get; init; } = userId;
}
