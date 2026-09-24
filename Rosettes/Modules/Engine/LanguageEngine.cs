using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rosettes.Core;

namespace Rosettes.Modules.Engine;

public static class LanguageEngine
{
    private const string ApiBaseUrl = "https://mmip-be.markski.ar/v1";
    private const string Model = "z-ai/glm-5.2";
    private const int MaxCompletionTokens = 4_096;
    private const int MaxContextCharacters = 200_000;
    private const int CompactAtCharacters = 160_000;
    private const int CompactTargetCharacters = 100_000;
    private const int MaxCompactCharacters = 190_000;
    private const int MaxRetries = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private sealed class ConversationState
    {
        public SemaphoreSlim TurnLock { get; } = new(1, 1);
        public List<JObject> History { get; } = [];
        public DateTimeOffset? StartedAt { get; set; }
        public PendingChat? PendingChat { get; set; }
        public PendingCompaction? PendingCompaction { get; set; }
    }

    private sealed record PendingChat(string Message, ulong UserId, string AttributedMessage, string RequestId, string Body, bool IsNewChat);
    private sealed record PendingCompaction(int MessageCount, string RequestId, string Body);

    private sealed class MmipResponse
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
        public string? Content { get; init; }
        public string? Status { get; init; }
        public JArray? Messages { get; init; }
        public string? Summary { get; init; }
        [JsonProperty("messages_compacted")]
        public int? MessagesCompacted { get; init; }
    }

    private sealed record RequestResult(HttpStatusCode StatusCode, string Body, MmipResponse? Data);

    private static readonly ConcurrentDictionary<ulong, ConversationState> Conversations = new();

    public static async Task<(bool, bool, string)> GetResponseAsync(ulong channelId, ulong userId, string message, string userName)
    {
        var state = Conversations.GetOrAdd(channelId, static _ => new ConversationState());
        await state.TurnLock.WaitAsync();

        try
        {
            if (string.Equals(message.Trim(), "clear", StringComparison.OrdinalIgnoreCase))
            {
                bool hadConversation = state.History.Count > 0 || state.PendingChat is not null;
                state.History.Clear();
                state.StartedAt = null;
                state.PendingChat = null;
                state.PendingCompaction = null;
                return (false, false, hadConversation
                    ? "Context cleared: I have forgotten this channel's conversation."
                    : "There is no conversation context to clear.");
            }

            if (string.IsNullOrWhiteSpace(Settings.SystemPrompt))
                return (false, false, "Sorry, chat is unavailable because its system prompt could not be loaded.");

            if (state.PendingChat is not null)
            {
                if (state.PendingChat.Message != message || state.PendingChat.UserId != userId)
                    return (false, false, "The previous chat request has an uncertain outcome. Retry that same question before sending a new one, or use `/chat clear` to start over.");

                return await CompleteChatAsync(state);
            }

            if (state.PendingCompaction is not null && !await CompactAsync(state))
                return (false, false, "Sorry, I could not compact this channel's context. Please try again.");

            DateTimeOffset messageSentAt = DateTimeOffset.Now;
            state.StartedAt ??= messageSentAt;
            string safeName = string.IsNullOrWhiteSpace(userName) ? "unknown" : userName.Trim();
            string attributedMessage = $"[{safeName} at {messageSentAt:dd/MM/yyyy HH:mm:ss zzz}]: {message}";
            string systemPrompt = BuildSystemPrompt(state.StartedAt.Value);

            while (state.History.Count > 0 && GetContextLength(state.History, attributedMessage, systemPrompt) > CompactAtCharacters)
            {
                if (!await CompactAsync(state))
                    return (false, false, "Sorry, I could not compact this channel's context. Please try again.");
            }

            if (GetContextLength(state.History, attributedMessage, systemPrompt) > MaxContextCharacters)
                return (false, false, "Sorry, this question exceeds the chat context limit.");

            bool isNewChat = state.History.Count == 0;
            string requestBody = JsonConvert.SerializeObject(new
            {
                message = attributedMessage,
                model = Model,
                history = state.History,
                web_search = true,
                system_prompt = systemPrompt,
                @params = new { max_tokens = MaxCompletionTokens }
            });
            state.PendingChat = new PendingChat(message, userId, attributedMessage, Guid.NewGuid().ToString("N"), requestBody, isNewChat);
            return await CompleteChatAsync(state);
        }
        finally
        {
            state.TurnLock.Release();
        }
    }

    private static string BuildSystemPrompt(DateTimeOffset conversationStartedAt)
    {
        string temporalContext = $"Conversation started at: {conversationStartedAt:dd/MM/yyyy HH:mm:ss zzz}.\n" +
                                 $"Current date: {DateTimeOffset.Now:dd/MM/yyyy}. Dates use dd/MM/yyyy format.";
        return $"{temporalContext}\n\n{Settings.SystemPrompt}";
    }

    private static int GetContextLength(List<JObject> history, string message, string systemPrompt)
    {
        int length = message.Length + systemPrompt.Length;
        foreach (JObject entry in history)
            length += entry.ToString(Formatting.None).Length;
        return length;
    }

    private static async Task<(bool, bool, string)> CompleteChatAsync(ConversationState state)
    {
        PendingChat pending = state.PendingChat!;
        RequestResult? result = await SendRequestAsync($"{ApiBaseUrl}/chat", pending.RequestId, pending.Body);
        if (result is null)
            return (pending.IsNewChat, false, "Sorry, the chat request is still unresolved. Retry the same question.");

        state.PendingChat = null;
        MmipResponse? data = result.Data;
        if (result.StatusCode == HttpStatusCode.OK && data?.Success == true)
        {
            if (data.Messages is null || data.Messages.Any(entry => entry is not JObject))
            {
                Global.GenerateErrorMessage("mmip-response", "Completed stateless response did not contain valid messages.");
                return (pending.IsNewChat, false, "Sorry, I could not save this response's context.");
            }

            state.History.Add(new JObject { ["role"] = "user", ["content"] = pending.AttributedMessage });
            state.History.AddRange(data.Messages.Cast<JObject>().Select(entry => (JObject)entry.DeepClone()));

            if (GetContextLength(state.History, "", "") > CompactAtCharacters)
                await CompactAsync(state);

            string? responseText = !string.IsNullOrWhiteSpace(data.Content) ? data.Content : data.Message;
            if (string.IsNullOrWhiteSpace(responseText))
                return (pending.IsNewChat, false, "Sorry, the chat service returned an empty response.");

            return (pending.IsNewChat, true, responseText);
        }

        if (data?.Status is "incomplete" or "failed" && !string.IsNullOrWhiteSpace(data.Content))
            return (pending.IsNewChat, true, data.Content);

        Global.GenerateErrorMessage("mmip-response", $"Error: {result.StatusCode} - {result.Body}");
        return (pending.IsNewChat, false, "Sorry, I am unable to respond at this moment.");
    }

    private static async Task<bool> CompactAsync(ConversationState state)
    {
        if (state.PendingCompaction is null)
        {
            int keepRecent = state.History.Count > 2 ? 2 : 0;
            int limit = Math.Min(state.History.Count - keepRecent, 500);
            var messages = new List<object>();
            int formattedLength = 0;
            int remainingLength = GetContextLength(state.History, "", "");

            for (int i = 0; i < limit && remainingLength > CompactTargetCharacters; i++)
            {
                JObject entry = state.History[i];
                string role = entry.Value<string>("role") ?? "unknown";
                string content = entry["content"]?.Type == JTokenType.String
                    ? entry.Value<string>("content") ?? ""
                    : entry["content"]?.ToString(Formatting.None) ?? "";
                var metadata = (JObject)entry.DeepClone();
                metadata.Remove("role");
                metadata.Remove("content");
                if (metadata.HasValues)
                    content += $"\nMetadata: {metadata.ToString(Formatting.None)}";
                if (string.IsNullOrWhiteSpace(content))
                    content = entry.ToString(Formatting.None);

                int entryLength = role.Length + content.Length + 4;
                if (formattedLength + entryLength > MaxCompactCharacters)
                    break;

                messages.Add(new { role = role is "user" or "assistant" ? role : "assistant", content = role is "user" or "assistant" ? content : $"[{role}] {content}" });
                formattedLength += entryLength;
                remainingLength -= entry.ToString(Formatting.None).Length;
            }

            if (messages.Count == 0)
                return false;

            string body = JsonConvert.SerializeObject(new { messages, max_summary_tokens = 800 });
            state.PendingCompaction = new PendingCompaction(messages.Count, Guid.NewGuid().ToString("N"), body);
        }

        PendingCompaction pending = state.PendingCompaction;
        RequestResult? result = await SendRequestAsync($"{ApiBaseUrl}/compact", pending.RequestId, pending.Body);
        if (result is null)
            return false;

        state.PendingCompaction = null;
        if (result.StatusCode != HttpStatusCode.OK || result.Data?.Success != true ||
            string.IsNullOrWhiteSpace(result.Data.Summary) || result.Data.MessagesCompacted != pending.MessageCount)
        {
            Global.GenerateErrorMessage("mmip-compact", $"Error: {result.StatusCode} - {result.Body}");
            return false;
        }

        var summary = new JObject
        {
            ["role"] = "assistant",
            ["content"] = $"Summary of earlier conversation (historical context):\n{result.Data.Summary}"
        };
        int oldLength = state.History.Take(pending.MessageCount).Sum(entry => entry.ToString(Formatting.None).Length);
        if (summary.ToString(Formatting.None).Length >= oldLength)
            return false;

        state.History.RemoveRange(0, pending.MessageCount);
        state.History.Insert(0, summary);
        return true;
    }

    private static async Task<RequestResult?> SendRequestAsync(string url, string requestId, string body)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey);
                request.Headers.Add("Idempotency-Key", requestId);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await Global.HttpClient.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();
                MmipResponse? data = null;
                try
                {
                    data = JsonConvert.DeserializeObject<MmipResponse>(responseBody);
                }
                catch (JsonException ex)
                {
                    Global.GenerateErrorMessage("mmip-response", $"Invalid JSON response: {ex.Message}");
                }

                bool unresolved = response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable &&
                                  (responseBody.Contains("request_busy", StringComparison.OrdinalIgnoreCase) ||
                                   responseBody.Contains("request_uncertain", StringComparison.OrdinalIgnoreCase));
                if (!unresolved)
                    return new RequestResult(response.StatusCode, responseBody, data);
            }
            catch (HttpRequestException ex)
            {
                Global.GenerateErrorMessage("mmip-response", $"Connection failed for request {requestId}: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                Global.GenerateErrorMessage("mmip-response", $"Request timed out for request {requestId}: {ex.Message}");
            }

            if (attempt < MaxRetries)
                await Task.Delay(RetryDelay);
        }

        return null;
    }
}
