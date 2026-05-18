using System;
using System.Collections.Generic;
using JiraRest;
using PlaneRest;
using Serilog;

namespace Preesta
{
    /// <summary>
    /// Executes ready-to-send Plane REST mutations. Mirror of
    /// <see cref="GithubMutationExecutor"/> / <see cref="LinearMutationExecutor"/>
    /// but goes through the Plane gateway (REST, <c>X-API-Key</c> header) instead
    /// of a GraphQL endpoint. Implements <see cref="IHttpHandler"/> so it plugs
    /// into the same pipeline slot as Jira's REST self-update path; Plane and
    /// Jira live in separate keyed pipelines, so there's no handler collision.
    /// </summary>
    /// <remarks>
    /// Per-mutation failures (HTTP errors, parse errors) are logged and swallowed —
    /// one bad mutation must not stop the others, same contract as the Linear /
    /// GitHub / Jira executors.
    /// </remarks>
    internal class PlaneMutationExecutor : IHttpHandler
    {
        private readonly IPlaneGateway _gateway;
        private readonly ILogger _logger;

        public PlaneMutationExecutor(IPlaneGateway gateway, ILogger logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        public void HandleAll(IEnumerable<HttpRequest> requests)
        {
            foreach (var r in requests)
                Execute(r);
        }

        private void Execute(HttpRequest r)
        {
            try
            {
                var responseText = _gateway.Send(r.Verb, r.Uri, r.Body);
                _logger.Information("Plane mutation {Verb} {Uri} succeeded ({Length} bytes)",
                    r.Verb, r.Uri, responseText?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Plane mutation {Verb} {Uri} failed", r.Verb, r.Uri);
            }
        }
    }
}
