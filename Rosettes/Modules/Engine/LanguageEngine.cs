using OpenAI.Chat;
using Rosettes.Core;

namespace Rosettes.Modules.Engine;

public static class LanguageEngine
{
    private static readonly ChatClient GptClient = new(
        model: "gpt-5.2", 
        apiKey: Settings.OpenAi
    );

    private static readonly string[] FactNeedles = ["why", "how", "explain", "source", "what is", "when"];
    
    private static readonly Dictionary<(ulong userId, ulong channelId), List<ChatMessage>> ConversationContexts = [];
    
    public static async Task<(bool, bool, string)> GetResponseAsync(ulong userId, ulong channelId, string message)
    {
        List<ChatMessage> messages;
        bool isNewChat = false;
        
        if (message.Trim() is "clear")
        {
            if (ConversationContexts.TryGetValue((userId, channelId), out _))
            {
                ConversationContexts.Remove((userId, channelId));
                return (isNewChat, false, "Context cleared: I have forgotten our conversation.");
            }
        }

        // If the given user already had spoken in this channel, fetch their context.
        if (ConversationContexts.TryGetValue((userId, channelId), out var context))
        {
            messages = context;
            
            // We hold onto no more than 20 exchanges worth of context.
            if (messages.Count > 21)
            {
                // Skip the 1st 'cause that's the system prompt.
                messages.RemoveRange(1, 2);
            }
        }
        else
        {
            // Create new with system prompt
            messages = [
                ChatMessage.CreateSystemMessage(
                    $"Today's date is: {DateTime.Now:dd/MM/yyyy} in dd/mm/yyyy format.;\n{Settings.OpenAiPrompt}"
                    )
            ];
            isNewChat = true;
        }

        messages.Add(ChatMessage.CreateUserMessage(message));
        
        // Check if the message contains any of our fact-finding keywords.
        // For queries that require more accuracy we use a lower temp.
        bool containsAny = FactNeedles.Any(n => message.Contains(n, StringComparison.OrdinalIgnoreCase));
        
        float temperature = containsAny ? 0.2f : 0.4f;
        
        ChatCompletionOptions options = new()
        {
            Temperature = temperature,
        };

        try
        {
            ChatCompletion completion = await GptClient.CompleteChatAsync(messages, options);
            string response = completion.Content[0].Text;
            messages.Add(ChatMessage.CreateAssistantMessage(response));
            ConversationContexts[(userId, channelId)] = messages;
            return (isNewChat, true, response);
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("gpt-response", $"Error returning response: {ex.Message}");
            return (isNewChat, false, "Sorry, I am unable to respond at this moment.");
        }
    }
}
