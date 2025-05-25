using OpenAI.Chat;
using Rosettes.Core;

namespace Rosettes.Modules.Engine;

public static class LanguageEngine
{
    public static async Task<string> GetResponseAsync(string message)
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(Settings.OpenAiPrompt),
            ChatMessage.CreateUserMessage(message)
        };
        
        ChatCompletion completion = await GptClient.CompleteChatAsync(messages);
        
        try
        {
            return completion.Content[0].Text;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("gpt-response", $"Error generating response: {ex.Message}");
            return "Sorry, my mind ain't working right now.";
        }
    }

    private static readonly ChatClient  GptClient = new(
        model: "gpt-4.1-mini", 
        apiKey: Settings.OpenAi
    );
}