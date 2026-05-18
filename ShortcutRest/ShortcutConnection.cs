using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace ShortcutRest
{
    /// <summary>
    /// Thin wrapper over <see cref="HttpClient"/> for the Shortcut REST API v3.
    /// </summary>
    /// <remarks>
    /// Shortcut uses a custom <c>Shortcut-Token: &lt;token&gt;</c> header — neither
    /// the <c>Authorization: Bearer …</c> form (GitHub) nor the raw-value
    /// <c>Authorization: …</c> form (Linear). Token is generated at
    /// <c>app.shortcut.com/settings/account/api-tokens</c>.
    /// </remarks>
    public class ShortcutConnection : IShortcutGateway, IDisposable
    {
        public const string DefaultEndpoint = "https://api.app.shortcut.com";
        private const string TokenHeader = "Shortcut-Token";

        internal HttpClient Client { get; }
        internal Uri BaseUri { get; }

        public ShortcutConnection(string token, HttpClient? httpClient = null)
            : this(token, DefaultEndpoint, httpClient)
        {
        }

        public ShortcutConnection(string token, string endpoint, HttpClient? httpClient = null)
        {
            Client = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            BaseUri = new Uri(endpoint.EndsWith("/") ? endpoint : endpoint + "/");
            // Header is set once and re-used for every request from this connection.
            // Re-adding the same header would throw, so guard against double-setup
            // (in case the caller passed in a pre-configured HttpClient).
            if (!Client.DefaultRequestHeaders.Contains(TokenHeader))
                Client.DefaultRequestHeaders.Add(TokenHeader, token);
        }

        public JObject SearchStories(string query, int pageSize = 25)
        {
            // detail=slim cuts description + comments from each result — we don't render
            // either, and slim keeps the response light when a filter matches dozens of
            // stories.
            var url = $"api/v3/search/stories?query={Uri.EscapeDataString(query)}" +
                      $"&page_size={pageSize}&detail=slim";
            var text = Get(url);
            return JObject.Parse(text);
        }

        public JArray GetMembers()
        {
            var text = Get("api/v3/members");
            return JArray.Parse(text);
        }

        public JArray GetWorkflows()
        {
            var text = Get("api/v3/workflows");
            return JArray.Parse(text);
        }

        public JObject GetCurrentMember()
        {
            var text = Get("api/v3/member");
            return JObject.Parse(text);
        }

        public void Send(string verb, string path, string? body)
        {
            var trimmedPath = path.StartsWith("/") ? path.Substring(1) : path;
            var request = new HttpRequestMessage(new HttpMethod(verb.ToUpperInvariant()),
                new Uri(BaseUri, trimmedPath));
            if (!string.IsNullOrEmpty(body))
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = Client.SendAsync(request).GetAwaiter().GetResult();
            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Shortcut REST {verb} {path} returned error '{text}'", ex);
            }
        }

        private string Get(string path)
        {
            var response = Client.GetAsync(new Uri(BaseUri, path)).GetAwaiter().GetResult();
            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Shortcut REST GET {path} returned error '{text}' from base '{BaseUri}'", ex);
            }
            return text;
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
