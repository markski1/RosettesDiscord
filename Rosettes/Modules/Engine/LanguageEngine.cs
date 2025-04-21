using OpenAI.Chat;
using Rosettes.Core;

namespace Rosettes.Modules.Engine;

public static class LanguageEngine
{
    private static readonly ChatClient  GptClient = new(
            model: "gpt-4.1-nano", 
            apiKey: Settings.OpenAi
        );

    public static async Task<string> GetResponseAsync(string message)
    {
        ChatCompletion completion = await GptClient.CompleteChatAsync(message);
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
}