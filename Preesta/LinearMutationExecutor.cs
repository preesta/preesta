using System;
using System.Collections.Generic;
using LinearGraphQL;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Preesta
{
    /// <summary>
    /// Executes ready-to-send Linear GraphQL mutations. Reuses the same
    /// <see cref="LinearConnection"/> instance as <see cref="LinearIssueSource"/>
    /// (single HttpClient, single auth header) so no separate authentication setup
    /// is needed.
    /// </summary>
    /// <remarks>
    /// Per-mutation failures (HTTP errors, GraphQL <c>errors</c> envelope) are
    /// logged and swallowed — one bad mutation must not stop the others, just
    /// like Jira's <c>callRest</c> path.
    /// </remarks>
    internal class LinearMutationExecutor : IGraphQLMutationHandler
    {
        private readonly ILinearGateway _gateway;
        private readonly ILogger _logger;

        public LinearMutationExecutor(ILinearGateway gateway, ILogger logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        public void HandleAll(IEnumerable<string> mutationBodies)
        {
            foreach (var body in mutationBodies)
            {
                Execute(body);
            }
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
                _logger.Error(ex, "Linear mutation failed at HTTP layer: {Mutation}", Truncate(mutationBody));
                return;
            }

            var errors = response?["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                _logger.Error("Linear mutation returned GraphQL errors: {Errors} for {Mutation}",
                    errors.ToString(Newtonsoft.Json.Formatting.None),
                    Truncate(mutationBody));
                return;
            }

            _logger.Information("Linear mutation succeeded: {Mutation}", Truncate(mutationBody));
        }

        private static string Truncate(string s) =>
            s.Length <= 200 ? s : s.Substring(0, 197) + "…";
    }
}
