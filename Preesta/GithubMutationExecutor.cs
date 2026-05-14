using System;
using System.Collections.Generic;
using GithubGraphQL;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Preesta
{
    /// <summary>
    /// Executes ready-to-send GitHub GraphQL mutations. Mirror of
    /// <see cref="LinearMutationExecutor"/> against the GitHub gateway.
    /// Reuses the same <see cref="GithubConnection"/> as
    /// <see cref="GithubIssueSource"/> (single HttpClient, single auth header).
    /// </summary>
    /// <remarks>
    /// Per-mutation failures (HTTP errors, GraphQL <c>errors</c> envelope) are
    /// logged and swallowed — one bad mutation must not stop the others.
    /// </remarks>
    internal class GithubMutationExecutor : IGraphQLMutationHandler
    {
        private readonly IGithubGateway _gateway;
        private readonly ILogger _logger;

        public GithubMutationExecutor(IGithubGateway gateway, ILogger logger)
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
                _logger.Error(ex, "GitHub mutation failed at HTTP layer: {Mutation}", Truncate(mutationBody));
                return;
            }

            var errors = response?["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                _logger.Error("GitHub mutation returned GraphQL errors: {Errors} for {Mutation}",
                    errors.ToString(Newtonsoft.Json.Formatting.None),
                    Truncate(mutationBody));
                return;
            }

            _logger.Information("GitHub mutation succeeded: {Mutation}", Truncate(mutationBody));
        }

        private static string Truncate(string s) =>
            s.Length <= 200 ? s : s.Substring(0, 197) + "…";
    }
}
