using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace PlaneRest
{
    /// <summary>
    /// Thin wrapper over <see cref="HttpClient"/> that talks to Plane's REST API.
    /// </summary>
    /// <remarks>
    /// Plane authenticates with a custom <c>X-API-Key</c> header (not <c>Authorization</c>);
    /// the token format is <c>plane_api_&lt;random&gt;</c>. Plane Cloud uses
    /// <c>https://api.plane.so/</c>; self-hosted instances configure their own base URL.
    /// Workspace slug is part of every path segment and stays the same across all
    /// requests, so it's bound to the connection rather than passed per-call.
    /// </remarks>
    public class PlaneConnection : IPlaneGateway, IDisposable
    {
        public const string DefaultApiBase = "https://api.plane.so/";

        internal HttpClient Client { get; }
        internal Uri ApiBase { get; }
        internal string WorkspaceSlug { get; }

        public PlaneConnection(string apiKey, string workspaceSlug, HttpClient? httpClient = null)
            : this(apiKey, workspaceSlug, DefaultApiBase, httpClient)
        {
        }

        public PlaneConnection(string apiKey, string workspaceSlug, string apiBase, HttpClient? httpClient = null)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("Plane API key is required", nameof(apiKey));
            if (string.IsNullOrEmpty(workspaceSlug))
                throw new ArgumentException("Plane workspace slug is required", nameof(workspaceSlug));

            Client = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            ApiBase = new Uri(apiBase.EndsWith("/") ? apiBase : apiBase + "/");
            WorkspaceSlug = workspaceSlug;

            if (!Client.DefaultRequestHeaders.Contains("X-API-Key"))
                Client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }

        public JObject ListWorkItems(string projectId, IReadOnlyDictionary<string, string> queryParams)
        {
            if (string.IsNullOrEmpty(projectId))
                throw new ArgumentException("projectId is required", nameof(projectId));

            var path = $"api/v1/workspaces/{WorkspaceSlug}/projects/{projectId}/work-items/";
            var uri = BuildUri(path, queryParams);
            var text = SendInternal("GET", uri, body: null);
            return JObject.Parse(text);
        }

        public JArray ListWorkspaceMembers()
        {
            var path = $"api/v1/workspaces/{WorkspaceSlug}/members/";
            var uri = BuildUri(path, queryParams: null);
            var text = SendInternal("GET", uri, body: null);
            // Plane's members endpoint historically returns a bare JSON array.
            // Some self-hosted versions wrap it as { results: [...] } — handle both.
            var token = JToken.Parse(text);
            if (token is JArray arr) return arr;
            if (token is JObject obj && obj["results"] is JArray paged) return paged;
            return new JArray();
        }

        public string Send(string verb, Uri uri, string? body) =>
            SendInternal(verb, uri, body);

        public void Dispose() => Client.Dispose();

        private Uri BuildUri(string path, IReadOnlyDictionary<string, string>? queryParams)
        {
            var b = new UriBuilder(new Uri(ApiBase, path));
            if (queryParams != null && queryParams.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var kv in queryParams)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    if (sb.Length > 0) sb.Append('&');
                    sb.Append(Uri.EscapeDataString(kv.Key));
                    sb.Append('=');
                    sb.Append(Uri.EscapeDataString(kv.Value ?? string.Empty));
                }
                b.Query = sb.ToString();
            }
            return b.Uri;
        }

        private string SendInternal(string verb, Uri uri, string? body)
        {
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(verb.ToUpperInvariant()), uri);
            if (!string.IsNullOrEmpty(body))
                request.Content = new StringContent(body!, Encoding.UTF8, "application/json");

            var response = Client.SendAsync(request).GetAwaiter().GetResult();
            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Plane API returned error '{text}' from url '{uri}'", ex);
            }
            return text;
        }
    }
}
