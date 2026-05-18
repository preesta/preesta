using System;
using System.Collections.Generic;
using JiraRest;
using Serilog;
using ShortcutRest;

namespace Preesta
{
    /// <summary>
    /// Executes ready-to-send Shortcut REST mutations. REST equivalent of
    /// <see cref="LinearMutationExecutor"/> / <see cref="GithubMutationExecutor"/>,
    /// but adapts the existing <see cref="IHttpHandler"/> slot on
    /// <c>ReactionPipeline</c> (Shortcut has no GraphQL).
    /// </summary>
    /// <remarks>
    /// Re-uses the same <see cref="IShortcutGateway"/> instance as
    /// <see cref="ShortcutIssueSource"/> so reads and writes share one
    /// <c>HttpClient</c> + one <c>Shortcut-Token</c> header.
    /// Per-request failures are logged and swallowed — one bad mutation must not
    /// stop the others.
    /// </remarks>
    internal class ShortcutMutationExecutor : IHttpHandler
    {
        private readonly IShortcutGateway _gateway;
        private readonly ILogger _logger;

        public ShortcutMutationExecutor(IShortcutGateway gateway, ILogger logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        public void HandleAll(IEnumerable<HttpRequest> requests)
        {
            foreach (var request in requests)
                Execute(request);
        }

        private void Execute(HttpRequest request)
        {
            try
            {
                // The rule supplies an absolute URL (e.g.
                // https://api.app.shortcut.com/api/v3/stories/123); pull off the path
                // and hand it to the gateway, which re-prepends its configured base.
                // This keeps mutation routing through the same token-header pipeline
                // as the read path.
                var path = request.Uri.AbsolutePath;
                if (!string.IsNullOrEmpty(request.Uri.Query))
                    path += request.Uri.Query;

                _gateway.Send(request.Verb, path, request.Body);
                _logger.Information("Shortcut mutation succeeded: {Verb} {Path}", request.Verb, path);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Shortcut mutation failed: {Verb} {Uri}", request.Verb, request.Uri);
            }
        }
    }
}
