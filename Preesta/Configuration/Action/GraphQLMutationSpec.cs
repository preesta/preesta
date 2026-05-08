using static System.String;

namespace Preesta.Configuration.Action
{
    /// <summary>
    /// One GraphQL mutation to execute against an external service when a rule fires.
    /// Currently used by Linear self-update; the same shape can serve any GraphQL
    /// integration (e.g. GitHub GraphQL) in the future.
    /// </summary>
    /// <remarks>
    /// The body is a raw GraphQL mutation string. Marker substitution is identical to
    /// REST mutations — placeholders like <c>{{@issueId}}</c>, <c>{{@issueKey}}</c>,
    /// <c>{{@assignee.email}}</c> etc. are replaced before the request is sent.
    /// </remarks>
    public class GraphQLMutationSpec
    {
        /// <summary>Full GraphQL mutation body, including the <c>mutation { ... }</c> wrapper.</summary>
        public string MutationBody { get; set; } = Empty;
    }
}
