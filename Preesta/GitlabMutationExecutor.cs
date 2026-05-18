using System;
using System.Collections.Generic;
using GitlabGraphQL;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Preesta
{
    /// <summary>
    /// Executes ready-to-send GitLab GraphQL mutations. Mirror of
    /// <see cref="LinearMutationExecutor"/> / <see cref="GithubMutationExecutor"/>
    /// against the GitLab gateway. Reuses the same <see cref="GitlabConnection"/>
    /// as <see cref="GitlabIssueSource"/> (single HttpClient, single auth header).
    /// </summary>
    /// <remarks>
    /// Per-mutation failures (HTTP errors, GraphQL <c>errors</c> envelope) are
    /// logged and swallowed — one bad mutation must not stop the others.
    /// </remarks>
    internal class GitlabMutationExecutor : IGraphQLMutationHandler
    {
        private readonly IGitlabGateway _gateway;
        private readonly ILogger _logger;

        public GitlabMutationExecutor(IGitlabGateway gateway, ILogger logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        public void HandleAll(IEnumerable<string> mutationBodies)
        {
            foreach (var body in mutationBodies)
                Execute(body);
        }

        private void Execute(string mutationBody)
        {
            JObject? response = null;
            try
            {
                response = _gateway.Query(mutationBody, variables: null);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "GitLab mutation failed at HTTP layer: {Mutation}", Truncate(mutationBody));
                return;
            }

            var errors = response?["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                _logger.Error("GitLab mutation returned GraphQL errors: {Errors} for {Mutation}",
                    errors.ToString(Newtonsoft.Json.Formatting.None),
                    Truncate(mutationBody));
                return;
            }

            _logger.Information("GitLab mutation succeeded: {Mutation}", Truncate(mutationBody));
        }

        private static string Truncate(string s) =>
            s.Length <= 200 ? s : s.Substring(0, 197) + "…";
    }
}
