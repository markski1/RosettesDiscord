using OpenAI.Chat;
using Rosettes.Core;

namespace Rosettes.Modules.Engine;

public static class LanguageEngine
{
    private static readonly ChatClient GptClient = new(
        model: "gpt-4.1-mini", 
        apiKey: Settings.OpenAi
    );
    
    private static readonly Dictionary<(ulong userId, ulong channelId), List<ChatMessage>> ConversationContexts = [];
    
    public static async Task<(bool, string)> GetResponseAsync(ulong userId, ulong channelId, string message)
    {
        List<ChatMessage> messages;

        // If the given user already had spoken in this channel, fetch their context.
        if (ConversationContexts.TryGetValue((userId, channelId), out var context))
        {
            messages = context;
            
            // We hold onto no more than 10 exchanges worth of context.
            if (messages.Count > 11)
            {
                // Skip the 1st 'cause that's the system prompt.
                messages.RemoveRange(1, 3);
            }
        }
        else
        {
            // Create new with system prompt
            messages = [
                ChatMessage.CreateSystemMessage(Settings.OpenAiPrompt)
            ];
        }

        // Add the user's query to the message list and request a completion.
        messages.Add(ChatMessage.CreateUserMessage(message));
        ChatCompletion completion = await GptClient.CompleteChatAsync(messages);

        try
        {
            string response = completion.Content[0].Text;
            // Add the response to the stored context.
            messages.Add(ChatMessage.CreateAssistantMessage(response));
            ConversationContexts[(userId, channelId)] = messages;
            return (true, response);
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("gpt-response", $"Error returning response: {ex.Message}");
            return (false, "Sorry, I am unable to respond at this moment.");
        }
    }
}
