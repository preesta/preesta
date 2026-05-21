using System;
using System.Collections.Generic;
using System.Linq;
using Preesta.Configuration.Action;
using Serilog;

namespace Preesta.Data.Supplying
{
    /// <summary>
    /// Mirrors <see cref="JqlSupplier"/> but pulls issues from <see cref="LinearIssueSource"/>
    /// instead of Jira. The base <see cref="IssueSupplier{TRule}.JiraService"/> is still
    /// required by the inherited grouping logic; it's only consulted for
    /// <c>AdditionalPredicateName</c> resolution, which Linear rules don't currently use.
    /// </summary>
    internal class LinearIssueSupplier : IssueSupplier<LinearRule>
    {
        private readonly LinearIssueSource _source;
        private readonly ILogger _logger;

        public LinearIssueSupplier(
            LinearIssueSource source,
            IJiraService? jiraService,
            IEnumerable<LinearRule> rules,
            ILogger logger)
            : base(jiraService, rules)
        {
            _source = source;
            _logger = logger;
        }

        protected override Issue[] GetIssues(LinearRule rule)
        {
            try
            {
                return _source.GetIssues(rule);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to get issues from Linear for rule: {@rule}", rule);
                return Array.Empty<Issue>();
            }
        }

        /// <summary>
        /// Mirrors <see cref="JqlSupplier.Enrich"/>: stashes the rule's filter-mode info on
        /// <see cref="PackageBase.Properties"/> so the formatter can render a "what produced
        /// this list" line in the digest. Linear's UI doesn't encode filter state in the
        /// URL (verified live — only saved views have a permanent shareable URL), so we
        /// only set <c>LinearViewName</c>+<c>LinearViewId</c> for the viewId mode; the
        /// other modes get the raw prompt or filter JSON to show, with no link.
        /// </summary>
        /// <summary>
        /// Linear's <c>mutations:</c> are GraphQL, not REST. Replace the base
        /// implementation entirely — base would have produced empty REST packages
        /// from the always-empty inherited <see cref="Rule.Mutations"/>; we instead
        /// wrap each <see cref="LinearRule.GraphQLMutations"/> entry as a
        /// <see cref="GraphQLMutation"/> reaction.
        /// </summary>
        protected override PackageBase[] GetMutationPackages((LinearRule rule, Issue[] issues)[] sets)
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

        protected internal override PackageBase Enrich(PackageBase basePackage, LinearRule rule)
        {
            if (!string.IsNullOrEmpty(rule.Filter))
                basePackage.Properties["LinearFilter"] = rule.Filter!;
            if (rule.FilterRaw != null)
                basePackage.Properties["LinearFilterRaw"] = rule.FilterRaw.ToString(Newtonsoft.Json.Formatting.None);
            if (!string.IsNullOrEmpty(rule.ViewId))
            {
                basePackage.Properties["LinearViewId"] = rule.ViewId!;
                // Best-effort lookup of the human-readable view name. We piggyback on the
                // same `customView(id:){ name issues { ... } }` request that
                // LinearIssueSource.GetIssues already issues for viewId rules; if the
                // request hasn't been made yet (or failed), we fall back to the id below.
                var viewName = _source.GetCachedViewName(rule.ViewId!);
                basePackage.Properties["LinearViewName"] = viewName ?? rule.ViewId!;
            }
            return basePackage;
        }
    }
}
