using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GithubGraphQL
{
    /// <summary>
    /// Thin wrapper over <see cref="HttpClient"/> that POSTs GraphQL queries to GitHub's API.
    /// </summary>
    /// <remarks>
    /// GitHub Personal Access Tokens (both classic and fine-grained) use the standard
    /// <c>Authorization: Bearer &lt;token&gt;</c> header — unlike Linear, which expects
    /// the key raw without a prefix.
    /// GitHub also requires a <c>User-Agent</c> header on every request.
    /// </remarks>
    public class GithubConnection : IGithubGateway, IDisposable
    {
        public const string DefaultEndpoint = "https://api.github.com/graphql";

        internal HttpClient Client { get; }
        internal Uri Endpoint { get; }

        public GithubConnection(string token, HttpClient? httpClient = null)
            : this(token, DefaultEndpoint, httpClient)
        {
        }

        public GithubConnection(string token, string endpoint, HttpClient? httpClient = null)
        {
            Client = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            Endpoint = new Uri(endpoint);
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            if (Client.DefaultRequestHeaders.UserAgent.Count == 0)
                Client.DefaultRequestHeaders.UserAgent.ParseAdd("Preesta/1.0");
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
                    $"GitHub GraphQL endpoint returned error '{text}' from url '{Endpoint}'", ex);
            }

            return JObject.Parse(text);
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
