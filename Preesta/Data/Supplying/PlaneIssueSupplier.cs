using System;
using System.Collections.Generic;
using Preesta.Configuration.Action;
using Serilog;

namespace Preesta.Data.Supplying
{
    /// <summary>
    /// Mirrors <see cref="GithubIssueSupplier"/> / <see cref="LinearIssueSupplier"/>
    /// but pulls work items from <see cref="PlaneIssueSource"/>. The base
    /// <see cref="IssueSupplier{TRule}.JiraService"/> is required by the inherited
    /// grouping logic; it's only consulted for <c>AdditionalPredicateName</c>
    /// resolution, which Plane rules don't currently use.
    /// </summary>
    /// <remarks>
    /// Plane mutations are REST (PATCH / POST), not GraphQL, so we use the base
    /// <see cref="IssueSupplier{TRule}.GetMutationPackages"/> implementation which
    /// emits <see cref="SelfUpdate"/> packages from <see cref="Rule.Mutations"/>
    /// (verb/urlPattern/body) — the same path Jira uses. The Plane-specific
    /// <see cref="PlaneMutationExecutor"/> handles authentication via the Plane
    /// gateway.
    /// </remarks>
    internal class PlaneIssueSupplier : IssueSupplier<PlaneRule>
    {
        private readonly PlaneIssueSource _source;
        private readonly ILogger _logger;
        private readonly string _workspaceSlug;

        public PlaneIssueSupplier(
            PlaneIssueSource source,
            IJiraService jiraService,
            IEnumerable<PlaneRule> rules,
            ILogger logger,
            string workspaceSlug)
            : base(jiraService, rules)
        {
            _source = source;
            _logger = logger;
            _workspaceSlug = workspaceSlug;
        }

        protected override Issue[] GetIssues(PlaneRule rule)
        {
            try
            {
                return _source.GetIssues(rule);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to get issues from Plane for rule: {@rule}", rule);
                return Array.Empty<Issue>();
            }
        }

        protected internal override PackageBase Enrich(PackageBase basePackage, PlaneRule rule)
        {
            if (!string.IsNullOrEmpty(rule.ProjectId))
            {
                basePackage.Properties["PlaneProjectId"] = rule.ProjectId!;
                // Round-trip link to the project's work-items page. Plane encodes
                // active filters in a URL fragment (display_filters base64) that
                // isn't documented for external generation, so the link drops the
                // chip filters and just points at the project — the recipient sees
                // the project's current state and can filter in the UI.
                if (!string.IsNullOrEmpty(_workspaceSlug))
                    basePackage.Properties["PlaneSearchUri"] =
                        $"https://app.plane.so/{_workspaceSlug}/projects/{rule.ProjectId}/issues/";
            }
            if (rule.Filter != null && rule.Filter.Count > 0)
            {
                // Render the filter as a one-line "k=v, k=v" string for the
                // human-readable digest header. Keys are sorted so the output is
                // deterministic regardless of YAML mapping order.
                var parts = new List<string>();
                foreach (var k in new System.Collections.Generic.SortedSet<string>(rule.Filter.Keys))
                    parts.Add($"{k}={rule.Filter[k]}");
                basePackage.Properties["PlaneFilter"] = string.Join(", ", parts);
            }
            return basePackage;
        }
    }
}
