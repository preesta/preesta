using System.Collections.Generic;

namespace Preesta
{
    /// <summary>
    /// Executes GraphQL mutations produced by rule pipelines (Linear, GitHub).
    /// Parallel to <see cref="IHttpHandler"/>, which executes REST requests for Jira.
    /// </summary>
    public interface IGraphQLMutationHandler
    {
        /// <summary>
        /// Issue every mutation body, in order. Each body is a complete GraphQL
        /// <c>mutation { ... }</c> string, with all <c>{{@…}}</c> markers already
        /// substituted by the pipeline. Implementations should log per-mutation
        /// failures (HTTP error, GraphQL <c>errors</c> envelope) and continue —
        /// never throw.
        /// </summary>
        void HandleAll(IEnumerable<string> mutationBodies);
    }
}
