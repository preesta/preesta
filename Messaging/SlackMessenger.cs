using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;

namespace Messaging
{
    /// <summary>
    /// Sends personal Slack DMs via the <c>chat.postMessage</c> Web API. Mirror of
    /// <see cref="TelegramMessenger"/> — one bot token, per-message routing, no
    /// channels and no incoming webhooks.
    /// </summary>
    /// <remarks>
    /// <para>Slack auto-creates a DM IM channel from the bot to a user the first
    /// time the bot posts to that user's ID, so passing a Slack user ID
    /// (<c>Uxxx…</c>) as <c>channel</c> Just Works.</para>
    /// <para>Slack's <c>chat.postMessage</c> always returns HTTP 200; success vs.
    /// failure lives in the JSON body's <c>ok</c> field. We treat
    /// <c>{ok:false}</c> as an error (logged, swallowed) — same as a non-2xx
    /// response — so a single bad user ID doesn't take down the whole digest.</para>
    /// </remarks>
    public class SlackMessenger : IMessenger
    {
        public const string DefaultEndpoint = "https://slack.com/api/chat.postMessage";

        private readonly string _botToken;
        private readonly HttpClient _httpClient;
        private readonly ILogger? _logger;
        private readonly string _endpoint;

        public SlackMessenger(string botToken, HttpClient? httpClient = null, ILogger? logger = null)
            : this(botToken, DefaultEndpoint, httpClient, logger)
        {
        }

        /// <summary>
        /// Overload exposing the endpoint URL for testing against an in-process mock server.
        /// </summary>
        public SlackMessenger(string botToken, string endpoint, HttpClient? httpClient = null, ILogger? logger = null)
        {
            _botToken = botToken;
            _httpClient = httpClient ?? new HttpClient();
            _logger = logger;
            _endpoint = endpoint;
        }

        public void Send(Message data)
        {
            var userId = data.To.Trim();
            if (string.IsNullOrEmpty(userId)) return;

            var text = !string.IsNullOrEmpty(data.TextBody) ? data.TextBody : data.Body;
            if (string.IsNullOrEmpty(text)) return;

            var payload = JsonSerializer.Serialize(new
            {
                channel = userId,
                text,
                mrkdwn = true
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _botToken);

            HttpResponseMessage response;
            try
            {
                response = _httpClient.SendAsync(request).GetAwaiter().GetResult();
            }
            catch (System.Exception ex)
            {
                _logger?.Error(ex, "Slack chat.postMessage transport error for user {UserId}", userId);
                return;
            }

            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                _logger?.Error("Slack chat.postMessage HTTP {Status} for user {UserId}: {Body}",
                    (int)response.StatusCode, userId, body);
                return;
            }

            // 200 OK doesn't imply success — Slack reports app-level errors via {ok:false, error:"..."}.
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
                {
                    var errorCode = doc.RootElement.TryGetProperty("error", out var err) ? err.GetString() : "unknown";
                    _logger?.Error("Slack chat.postMessage failed for user {UserId}: {Error}", userId, errorCode);
                }
            }
            catch (JsonException ex)
            {
                _logger?.Error(ex, "Slack chat.postMessage returned non-JSON body for user {UserId}: {Body}",
                    userId, body);
            }
        }

        public void SendAll(IEnumerable<Message> messages)
        {
            foreach (var message in messages)
            {
                Send(message);
            }
        }
    }
}
