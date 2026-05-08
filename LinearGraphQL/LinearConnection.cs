using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LinearGraphQL
{
    /// <summary>
    /// Thin wrapper over <see cref="HttpClient"/> that POSTs GraphQL queries to Linear's API.
    /// </summary>
    /// <remarks>
    /// Linear personal API keys are placed verbatim in the <c>Authorization</c> header
    /// (no <c>Bearer</c> prefix) — see https://developers.linear.app/docs/graphql/working-with-the-graphql-api#authentication
    /// </remarks>
    public class LinearConnection : ILinearGateway, IDisposable
    {
        public const string DefaultEndpoint = "https://api.linear.app/graphql";

        internal HttpClient Client { get; }
        internal Uri Endpoint { get; }

        public LinearConnection(string apiKey, HttpClient? httpClient = null)
            : this(apiKey, DefaultEndpoint, httpClient)
        {
        }

        /// <summary>
        /// Overload exposing the endpoint URI for testing against an in-process mock server.
        /// </summary>
        public LinearConnection(string apiKey, string endpoint, HttpClient? httpClient = null)
        {
            Client = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            Endpoint = new Uri(endpoint);
            // Linear personal API keys go RAW in the Authorization header — no "Bearer " prefix.
            Client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);
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
                    $"Linear GraphQL endpoint returned error '{text}' from url '{Endpoint}'", ex);
            }

            return JObject.Parse(text);
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
