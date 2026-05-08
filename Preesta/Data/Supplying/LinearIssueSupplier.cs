using System;
using System.Collections.Generic;
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
            IJiraService jiraService,
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
    }
}
