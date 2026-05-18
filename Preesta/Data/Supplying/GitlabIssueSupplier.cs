using System;
using System.Collections.Generic;
using System.Linq;
using Preesta.Configuration.Action;
using Serilog;

namespace Preesta.Data.Supplying
{
    /// <summary>
    /// Mirrors <see cref="LinearIssueSupplier"/> / <see cref="GithubIssueSupplier"/> but
    /// pulls issues from <see cref="GitlabIssueSource"/>. The base
    /// <see cref="IssueSupplier{TRule}.JiraService"/> is required by the inherited
    /// grouping logic; it's only consulted for <c>AdditionalPredicateName</c> resolution,
    /// which GitLab rules don't currently use.
    /// </summary>
    internal class GitlabIssueSupplier : IssueSupplier<GitlabRule>
    {
        private readonly GitlabIssueSource _source;
        private readonly ILogger _logger;

        public GitlabIssueSupplier(
            GitlabIssueSource source,
            IJiraService jiraService,
            IEnumerable<GitlabRule> rules,
            ILogger logger)
            : base(jiraService, rules)
        {
            _source = source;
            _logger = logger;
        }

        protected override Issue[] GetIssues(GitlabRule rule)
        {
            try
            {
                return _source.GetIssues(rule);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to get issues from GitLab for rule: {@rule}", rule);
                return Array.Empty<Issue>();
            }
        }

        /// <summary>
        /// GitLab's <c>mutations:</c> are GraphQL, not REST — replace the base
        /// implementation entirely (mirror of Linear / GitHub) so each
        /// <see cref="GitlabRule.GraphQLMutations"/> entry becomes a
        /// <see cref="GraphQLMutation"/> reaction package.
        /// </summary>
        protected override PackageBase[] GetMutationPackages((GitlabRule rule, Issue[] issues)[] sets)
        {
            return
            (
                from set in sets
                from spec in set.rule.GraphQLMutations
                let package = new Package<GraphQLMutation, Issue>
                {
                    Reaction = new GraphQLMutation { MutationBody = spec.MutationBody },
                    Items = set.issues
                }
                select package
            )
            .Cast<PackageBase>()
            .ToArray();
        }

        protected internal override PackageBase Enrich(PackageBase basePackage, GitlabRule rule)
        {
            if (rule.Filter != null && rule.Filter.HasAnyField)
            {
                basePackage.Properties["GitlabFilter"] = rule.Filter.ToHumanString();
                // Round-trip link to the GitLab dashboard pre-filtered to the same
                // chips — mirror of Jira's "Open in Jira →" and GitHub's "Open in
                // GitHub →" links. Hard-coded host: gitlab.com works for SaaS users;
                // self-hosted is a follow-up (would need the apiBase host derived
                // here, which the supplier currently doesn't see).
                var qs = rule.Filter.ToDashboardQueryString();
                if (!string.IsNullOrEmpty(qs))
                    basePackage.Properties["GitlabSearchUri"] = $"https://gitlab.com/dashboard/issues?{qs}";
            }
            return basePackage;
        }
    }
}
