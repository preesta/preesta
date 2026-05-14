using Newtonsoft.Json.Linq;

namespace GithubGraphQL
{
    /// <summary>
    /// Minimal GitHub GraphQL gateway used by Preesta. Mirror of
    /// <c>LinearGraphQL.ILinearGateway</c> — one method, raw envelope returned.
    /// </summary>
    public interface IGithubGateway
    {
        /// <summary>
        /// POSTs the given GraphQL query (and optional variables object) to GitHub and
        /// returns the raw JSON response envelope (<c>data</c> + optional <c>errors</c>).
        /// </summary>
        JObject Query(string graphqlQuery, object? variables = null);
    }
}
