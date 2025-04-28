using Discord;
using Rosettes.Core;

namespace Rosettes.Modules.Engine;

public static class AuthEngine
{
    public static async Task<bool> RequestApplicationAuth(string applicationName, string actionName, User user)
    {
        EmbedBuilder embed = await Global.MakeRosettesEmbed();
        embed.Title = "Authorization Request";
        embed.Description = "An application is requesting you authorize an action. " +
                            "Please read carefully. Ignore or deny this request if you don't know what it is.";
        
        embed.AddField("Application", applicationName);
        embed.AddField("Action", actionName);

        ComponentBuilder comps = new ComponentBuilder();
        
        comps.WithButton("Accept", $"auth_accept:{applicationName}:{actionName}", ButtonStyle.Success);
        comps.WithButton("Deny", $"auth_deny:{applicationName}:{actionName}", ButtonStyle.Danger);

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
