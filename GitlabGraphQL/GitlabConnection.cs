using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GitlabGraphQL
{
    /// <summary>
    /// Thin wrapper over <see cref="HttpClient"/> that POSTs GraphQL queries to GitLab's API.
    /// </summary>
    /// <remarks>
    /// GitLab Personal Access Tokens authenticate via either
    /// <c>PRIVATE-TOKEN: &lt;token&gt;</c> or the standard
    /// <c>Authorization: Bearer &lt;token&gt;</c> header. We use the latter for symmetry
    /// with the GitHub and Linear (sans-Bearer) clients in this codebase.
    /// The default endpoint targets GitLab.com SaaS; self-hosted instances pass a
    /// different base URL through the constructor (e.g.
    /// <c>https://gitlab.example.com/api/graphql</c>).
    /// </remarks>
    public class GitlabConnection : IGitlabGateway, IDisposable
    {
        public const string DefaultEndpoint = "https://gitlab.com/api/graphql";

        internal HttpClient Client { get; }
        internal Uri Endpoint { get; }

        public GitlabConnection(string token, HttpClient? httpClient = null)
            : this(token, DefaultEndpoint, httpClient)
        {
        }

        public GitlabConnection(string token, string endpoint, HttpClient? httpClient = null)
        {
            Client = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            Endpoint = new Uri(string.IsNullOrEmpty(endpoint) ? DefaultEndpoint : endpoint);
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public JObject Query(string graphqlQuery, object? variables = null)
        {
            var bodyObj = variables == null
                ? (object)new { query = graphqlQuery }
                : new { query = graphqlQuery, variables };
            var bodyJson = JsonConvert.SerializeObject(bodyObj);
            var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            var response = Client.PostAsync(Endpoint, content).GetAwaiter().GetResult();
            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"GitLab GraphQL endpoint returned error '{text}' from url '{Endpoint}'", ex);
            }

            return JObject.Parse(text);
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
