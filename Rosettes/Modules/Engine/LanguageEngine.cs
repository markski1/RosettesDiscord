using System.Text;
using Newtonsoft.Json;
using Rosettes.Core;

namespace Rosettes.Modules.Engine;

public static class LanguageEngine
{
    private const string ApiUrl = "https://mmip-be.markski.ar/v1/chat";
    private const string CompactUrl = "https://mmip-be.markski.ar/v1/compact";
    private const string Model = "z-ai/glm-4.7";
    private const string CompactModel = "google/gemini-3.1-flash-lite";

    private const int MaxChars = 400_000;
    private const int MaxSummaryTokens = 800;

    private sealed record ChatMessage(string Role, string Content);

    private static readonly Dictionary<ulong, List<ChatMessage>> ConversationContexts = [];

    public static async Task<(bool, bool, string)> GetResponseAsync(ulong channelId, string message, string userName)
    {
        List<ChatMessage> messages;
        bool isNewChat = false;

        if (message.Trim() is "clear")
        {
            if (ConversationContexts.TryGetValue(channelId, out _))
            {
                ConversationContexts.Remove(channelId);
                return (isNewChat, false, "Context cleared: I have forgotten this channel's conversation.");
            }
        }

        string safeName = string.IsNullOrWhiteSpace(userName) ? "unknown" : userName.Trim();
        string attributedMessage = $"[{safeName}]: {message}";

        if (ConversationContexts.TryGetValue(channelId, out var context))
        {
            messages = context;

            if (messages.Sum(m => m.Content.Length) > MaxChars && messages.Count > 1)
            {
                bool compacted = await TryCompactAsync(messages);
                if (!compacted)
                {
                    // Fallback: drop oldest user/assistant pairs (system prompt is at index 0 and survives).
                    while (messages.Sum(m => m.Content.Length) > MaxChars && messages.Count > 3)
                    {
                        messages.RemoveAt(1);
                        messages.RemoveAt(1);
                    }
                }
            }
        }
        else
        {
            messages = [
                new ChatMessage("system", $"Today's date is: {DateTime.Now:dd/MM/yyyy} in dd/MM/yyyy format.;\n{Settings.SystemPrompt}")
            ];
            isNewChat = true;
        }

        var requestBody = new
        {
            message = attributedMessage,
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

            messages.Add(new ChatMessage("user", attributedMessage));
            messages.Add(new ChatMessage("assistant", responseText));
            ConversationContexts[channelId] = messages;

            return (isNewChat, true, responseText);
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("mmip-response", $"Error returning response: {ex.Message}");
            return (isNewChat, false, "Sorry, I am unable to respond at this moment.");
        }
    }

    // Use MMIP's /v1/compact and keep the last 6 messages.
    private static async Task<bool> TryCompactAsync(List<ChatMessage> messages)
    {
        try
        {
            var requestBody = new
            {
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                model = CompactModel,
                max_summary_tokens = MaxSummaryTokens,
                @params = new { temperature = 0.2 }
            };

            var json = JsonConvert.SerializeObject(requestBody);

            var request = new HttpRequestMessage(HttpMethod.Post, CompactUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Settings.ApiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await Global.HttpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Global.GenerateErrorMessage("mmip-compact", $"Error: {response.StatusCode} - {responseBody}");
                return false;
            }

            dynamic? data = JsonConvert.DeserializeObject<dynamic>(responseBody);
            if (data is null)
            {
                Global.GenerateErrorMessage("mmip-compact", "Null response from /v1/compact");
                return false;
            }

            bool success = data.success ?? false;
            if (!success) return false;

            string summary = data.summary;
            if (string.IsNullOrWhiteSpace(summary) || summary.StartsWith("[MMIP]"))
            {
                // Probably out of funds, so fallback to deleting message pairs.
                return false;
            }

            var originalSystem = messages.Find(m => m.Role == "system")?.Content
                                 ?? $"Today's date is: {DateTime.Now:dd/MM/yyyy} in dd/MM/yyyy format.;";

            var recent = messages
                .Where(m => m.Role is "user" or "assistant")
                .TakeLast(6)
                .ToList();

            messages.Clear();
            messages.Add(new ChatMessage("system", originalSystem));
            messages.Add(new ChatMessage("system", $"Summary of prior conversation with the user:\n{summary}"));
            foreach (var turn in recent) messages.Add(turn);

            return true;
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("mmip-compact", $"Error compacting: {ex.Message}");
            return false;
        }
    }
}
