using static System.String;

namespace Preesta.Data.Supplying
{
    /// <summary>
    /// Pipeline reaction wrapper for one GraphQL mutation to be executed against an
    /// external service (Linear today). Parallel to <see cref="SelfUpdate"/>, which
    /// wraps a REST request for Jira.
    /// </summary>
    /// <remarks>
    /// Markers in <see cref="MutationBody"/> (e.g. <c>{{@issueId}}</c>) are replaced
    /// with issue context by the executor before posting to the GraphQL endpoint.
    /// </remarks>
    internal class GraphQLMutation
    {
        public string MutationBody { get; set; } = Empty;
    }
}
