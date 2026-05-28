using System;
using System.Collections.Generic;
using Preesta.Configuration;
using Microsoft.CSharp.RuntimeBinder;
using Serilog;

namespace Preesta.Data.Supplying
{
    internal class JqlSupplier : IssueSupplier<JqlRule>
    {
        private readonly ILogger _logger;

        public JqlSupplier(IJiraService jiraService, IEnumerable<JqlRule> rules, ILogger logger)
            : base(jiraService, rules)
        {
            _logger = logger;
        }

        protected override Issue[] GetIssues(JqlRule rule)
        {
            try
            {
                // JqlSupplier's own ctor takes a non-null IJiraService, so the
                // nullable base property is guaranteed non-null here.
                return JiraService!.GetIssuesForJql(rule.Filter);
            }
            catch (Exception e)
            {
                if (!(e is InvalidOperationException) && !(e is RuntimeBinderException))
                {
                    throw;
                }

                _logger.Error(e, "Failed to get issues from Jira Service for Jira rule: {@rule}", rule);
                return new Issue[] { };
            }
        }

        #region Overrides of IssueSupplier<JqlRule,JqlIssuePackage>

        protected internal override PackageBase Enrich(PackageBase basePackage, JqlRule rule)
        {
            // The Properties bag key stays "Jql" — downstream templates and the
            // mail digest reference it by that name to build the "Open in Jira"
            // link from the original filter string.
            basePackage.Properties["Jql"] = rule.Filter;
            return basePackage;
        }

        #endregion
    }
}