using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Messaging
{
    public class TelegramMessenger : IMessenger
    {
        private readonly string _botToken;
        private readonly HttpClient _httpClient;

        public TelegramMessenger(string botToken, HttpClient? httpClient = null)
        {
            _botToken = botToken;
            _httpClient = httpClient ?? new HttpClient();
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
            var response = _httpClient
                .PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content)
                .GetAwaiter().GetResult();

            response.EnsureSuccessStatusCode();
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
