using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Serilog;

namespace Messaging
{
    public class TelegramMessenger : IMessenger
    {
        private readonly string _botToken;
        private readonly HttpClient _httpClient;
        private readonly ILogger? _logger;

        public TelegramMessenger(string botToken, HttpClient? httpClient = null, ILogger? logger = null)
        {
            _botToken = botToken;
            _httpClient = httpClient ?? new HttpClient();
            _logger = logger;
        }

        public void Send(Message data)
        {
            var chatId = data.To.Trim();
            if (string.IsNullOrEmpty(chatId)) return;

            var text = !string.IsNullOrEmpty(data.TextBody) ? data.TextBody : data.Body;
            if (string.IsNullOrEmpty(text)) return;

            var payload = JsonSerializer.Serialize(new
            {
                chat_id = chatId,
                text,
                parse_mode = "HTML",
                disable_web_page_preview = true
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = _httpClient
                    .PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Telegram sendMessage transport error for chat {ChatId}", chatId);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                // Body usually carries Telegram's structured error (description, error_code).
                // Read it for the log; don't throw — one blocked user or bad chat ID never
                // aborts the rest of the digest.
                string body;
                try { body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); }
                catch { body = "(could not read body)"; }
                _logger?.Error("Telegram sendMessage returned {Status} for chat {ChatId}: {Body}",
                    (int)response.StatusCode, chatId, body);
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
