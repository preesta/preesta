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
                if (!string.IsNullOrEmpty(_workspaceSlug))
                    basePackage.Properties["PlaneSearchUri"] =
                        $"https://app.plane.so/{_workspaceSlug}/projects/{rule.ProjectId}/issues/";
            }
            if (rule.Filter != null && rule.Filter.Count > 0)
            {
                // Render only the chips that represent actual user-facing filters in
                // the digest header. `expand` is an API-shape directive (asks the
                // source to inline the state object) — confusing to surface.
                var parts = new List<string>();
                foreach (var k in new System.Collections.Generic.SortedSet<string>(rule.Filter.Keys))
                {
                    if (string.Equals(k, "expand", StringComparison.OrdinalIgnoreCase)) continue;
                    parts.Add($"{k}={rule.Filter[k]}");
                }
                if (parts.Count > 0)
                    basePackage.Properties["PlaneFilter"] = string.Join(", ", parts);
            }
            return basePackage;
        }
    }
}
