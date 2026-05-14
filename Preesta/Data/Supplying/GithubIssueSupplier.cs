using System;
using System.Collections.Generic;
using System.Linq;
using Preesta.Configuration.Action;
using Serilog;

namespace Preesta.Data.Supplying
{
    /// <summary>
    /// Mirrors <see cref="LinearIssueSupplier"/> but pulls issues from
    /// <see cref="GithubIssueSource"/> instead of Linear. The base
    /// <see cref="IssueSupplier{TRule}.JiraService"/> is required by the inherited
    /// grouping logic; it's only consulted for <c>AdditionalPredicateName</c>
    /// resolution, which GitHub rules don't currently use.
    /// </summary>
    internal class GithubIssueSupplier : IssueSupplier<GithubRule>
    {
        private readonly GithubIssueSource _source;
        private readonly ILogger _logger;

        public GithubIssueSupplier(
            GithubIssueSource source,
            IJiraService jiraService,
            IEnumerable<GithubRule> rules,
            ILogger logger)
            : base(jiraService, rules)
        {
            _source = source;
            _logger = logger;
        }

        protected override Issue[] GetIssues(GithubRule rule)
        {
            try
            {
                return _source.GetIssues(rule);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to get issues from GitHub for rule: {@rule}", rule);
                return Array.Empty<Issue>();
            }
        }

        /// <summary>
        /// GitHub's <c>mutations:</c> are GraphQL, not REST — replace the base
        /// implementation that produces empty REST packages from the always-empty
        /// inherited <see cref="Rule.Mutations"/>, and instead wrap each
        /// <see cref="GithubRule.GraphQLMutations"/> entry as a <see cref="GraphQLMutation"/>
        /// reaction.
        /// </summary>
        protected override PackageBase[] GetMutationPackages((GithubRule rule, Issue[] issues)[] sets)
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

        protected internal override PackageBase Enrich(PackageBase basePackage, GithubRule rule)
        {
            if (!string.IsNullOrEmpty(rule.Filter))
                basePackage.Properties["GithubFilter"] = rule.Filter!;
            return basePackage;
        }
    }
}
