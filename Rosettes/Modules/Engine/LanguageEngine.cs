using System.Text;
using Newtonsoft.Json;
using Rosettes.Core;

namespace Rosettes.Modules.Engine;

public static class LanguageEngine
{
    private const string ApiUrl = "https://mmip-be.markski.ar/v1/chat";
    private const string Model = "openai/gpt-5.4-mini";

    private sealed record ChatMessage(string Role, string Content);

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

        if (ConversationContexts.TryGetValue((userId, channelId), out var context))
        {
            messages = context;

            const int MaxChars = 120_000;
            while (messages.Sum(m => m.Content.Length) > MaxChars && messages.Count > 3)
            {
                messages.RemoveAt(1);
                messages.RemoveAt(1);
            }
        }
        else
        {
            messages = [
                new ChatMessage("system", $"Today's date is: {DateTime.Now:dd/MM/yyyy} in dd/mm/yyyy format.;\n{Settings.SystemPrompt}")
            ];
            isNewChat = true;
        }
        
        var requestBody = new
        {
            message,
            history = messages.Where(m => m.Role != "system").Select(m => new { role = m.Role, content = m.Content }),
            model = Model,
            web_search = true,
            system_prompt = messages.Find(m => m.Role == "system")?.Content ?? ""
        };

        var json = JsonConvert.SerializeObject(requestBody);
        
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Settings.ApiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await Global.HttpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Global.GenerateErrorMessage("mmip-response", $"Error: {response.StatusCode} - {responseBody}");
                return (isNewChat, false, "Sorry, I am unable to respond at this moment.");
            }

            dynamic? responseData = JsonConvert.DeserializeObject<dynamic>(responseBody);

            if (responseData is null)
            {
                Global.GenerateErrorMessage("mmip-response", "Null response from API");
                return (isNewChat, false, "Sorry, I am unable to respond at this moment.");
            }

            string responseText = responseData.message;

            messages.Add(new ChatMessage("user", message));
            messages.Add(new ChatMessage("assistant", responseText));
            ConversationContexts[(userId, channelId)] = messages;

            return (isNewChat, true, responseText);
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("mmip-response", $"Error returning response: {ex.Message}");
            return (isNewChat, false, "Sorry, I am unable to respond at this moment.");
        }
    }
}
