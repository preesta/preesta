using Newtonsoft.Json.Linq;

namespace GitlabGraphQL
{
    /// <summary>
    /// Minimal GitLab GraphQL gateway used by Preesta. Mirror of
    /// <c>LinearGraphQL.ILinearGateway</c> and <c>GithubGraphQL.IGithubGateway</c> —
    /// one method, raw envelope returned.
    /// </summary>
    public interface IGitlabGateway
    {
        /// <summary>
        /// POSTs the given GraphQL query (and optional variables object) to GitLab and
        /// returns the raw JSON response envelope (<c>data</c> + optional <c>errors</c>).
        /// </summary>
        JObject Query(string graphqlQuery, object? variables = null);
    }
}
