using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Rosettes.Core;

namespace Rosettes.Modules.Engine;

public static class LanguageEngine
{
    private const string ApiBaseUrl = "https://mmip-be.markski.ar/v1";
    private const string Model = "z-ai/glm-5.2";
    private const int MaxCompletionTokens = 4_096;
    private const int MaxBusyRetries = 2;
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromSeconds(1);

    private sealed class ConversationState
    {
        public SemaphoreSlim TurnLock { get; } = new(1, 1);
        public string? ConversationId { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
    }

    private sealed class MmipResponse
    {
        public bool Success { get; init; }
        public string? Uuid { get; init; }
        public string? Message { get; init; }
        public string? Content { get; init; }
        public string? Status { get; init; }
        public List<MmipImage>? Images { get; init; }
    }

    private sealed class MmipImage
    {
        public string? Alt { get; init; }
        public string? Url { get; init; }
    }

    private static readonly ConcurrentDictionary<ulong, ConversationState> Conversations = new();

    public static async Task<(bool, bool, string)> GetResponseAsync(ulong channelId, string message, string userName)
    {
        var state = Conversations.GetOrAdd(channelId, static _ => new ConversationState());
        await state.TurnLock.WaitAsync();

        try
        {
            if (string.Equals(message.Trim(), "clear", StringComparison.OrdinalIgnoreCase))
            {
                bool hadConversation = state.ConversationId is not null;
                state.ConversationId = null;
                state.StartedAt = null;
                string clearResponse = hadConversation
                    ? "Context cleared: I have forgotten this channel's conversation."
                    : "There is no conversation context to clear.";

                return (false, false, clearResponse);
            }

            if (string.IsNullOrWhiteSpace(Settings.SystemPrompt))
                return (false, false, "Sorry, chat is unavailable because its system prompt could not be loaded.");

            DateTimeOffset messageSentAt = DateTimeOffset.Now;
            string? conversationId = state.ConversationId;
            bool isNewChat = conversationId is null;
            if (conversationId is null)
            {
                conversationId = await CreateConversationAsync();
                if (conversationId is null)
                    return (true, false, "Sorry, I am unable to respond at this moment.");

                state.ConversationId = conversationId;
                state.StartedAt = messageSentAt;
            }

            string safeName = string.IsNullOrWhiteSpace(userName) ? "unknown" : userName.Trim();
            string attributedMessage = $"[{safeName} at {messageSentAt:dd/MM/yyyy HH:mm:ss zzz}]: {message}";
            string requestId = Guid.NewGuid().ToString("N");
            string requestBody = JsonConvert.SerializeObject(new
            {
                message = attributedMessage,
                model = Model,
                web_search = true,
                system_prompt = BuildSystemPrompt(state.StartedAt ?? messageSentAt),
                @params = new { max_tokens = MaxCompletionTokens }
            });

            return await SendChatAsync(conversationId, requestId, requestBody, isNewChat);
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

    private static async Task<string?> CreateConversationAsync()
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, $"{ApiBaseUrl}/conversations", "{}", null);
            using var response = await Global.HttpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();
            var data = DeserializeResponse(responseBody, "mmip-conversation");

            if (response.IsSuccessStatusCode && data?.Success == true && !string.IsNullOrWhiteSpace(data.Uuid))
                return data.Uuid;

            Global.GenerateErrorMessage("mmip-conversation", $"Error: {response.StatusCode} - {responseBody}");
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("mmip-conversation", $"Error creating conversation: {ex.Message}");
        }

        return null;
    }

    private static async Task<(bool, bool, string)> SendChatAsync(
        string conversationId,
        string requestId,
        string requestBody,
        bool isNewChat)
    {
        string url = $"{ApiBaseUrl}/conversations/{Uri.EscapeDataString(conversationId)}/chat";

        for (int attempt = 0; attempt <= MaxBusyRetries; attempt++)
        {
            try
            {
                using var request = CreateRequest(HttpMethod.Post, url, requestBody, requestId);
                using var response = await Global.HttpClient.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();
                var data = DeserializeResponse(responseBody, "mmip-response");

                if (response.IsSuccessStatusCode && data?.Success == true)
                {
                    string? responseText = FormatResponse(data);
                    if (!string.IsNullOrWhiteSpace(responseText))
                        return (isNewChat, true, responseText);
                }

                if (response.StatusCode == HttpStatusCode.Conflict &&
                    responseBody.Contains("request_busy", StringComparison.OrdinalIgnoreCase) &&
                    attempt < MaxBusyRetries)
                {
                    await Task.Delay(BusyRetryDelay);
                    continue;
                }

                if (data?.Status == "incomplete")
                {
                    string? partialResponse = FormatResponse(data, includeMessage: false);
                    if (!string.IsNullOrWhiteSpace(partialResponse))
                        return (isNewChat, true, partialResponse);
                }

                Global.GenerateErrorMessage("mmip-response", $"Error: {response.StatusCode} - {responseBody}");
                return (isNewChat, false, "Sorry, I am unable to respond at this moment.");
            }
            catch (HttpRequestException ex) when (attempt < MaxBusyRetries)
            {
                Global.GenerateErrorMessage("mmip-response", $"Connection failed; retrying request {requestId}: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (attempt < MaxBusyRetries)
            {
                Global.GenerateErrorMessage("mmip-response", $"Request timed out; retrying request {requestId}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("mmip-response", $"Error returning response: {ex.Message}");
                return (isNewChat, false, "Sorry, I am unable to respond at this moment.");
            }
        }

        return (isNewChat, false, "Sorry, I am unable to respond at this moment.");
    }

    private static string? FormatResponse(MmipResponse response, bool includeMessage = true)
    {
        string? text = response.Content ?? (includeMessage ? response.Message : null);
        var images = response.Images?
            .Where(image => !string.IsNullOrWhiteSpace(image.Url))
            .Select(image => string.IsNullOrWhiteSpace(image.Alt)
                ? $"Image: {image.Url}"
                : $"{image.Alt}: {image.Url}")
            .ToList();

        if (images is null || images.Count == 0)
            return text;

        string imageText = string.Join('\n', images);
        return string.IsNullOrWhiteSpace(text) ? imageText : $"{text}\n\n{imageText}";
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string body, string? requestId)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey);
        if (requestId is not null)
            request.Headers.Add("Idempotency-Key", requestId);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    private static MmipResponse? DeserializeResponse(string responseBody, string source)
    {
        try
        {
            return JsonConvert.DeserializeObject<MmipResponse>(responseBody);
        }
        catch (JsonException ex)
        {
            Global.GenerateErrorMessage(source, $"Invalid JSON response: {ex.Message}");
            return null;
        }
    }
}
