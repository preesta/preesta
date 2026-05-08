using System.Collections.Generic;
using System.Linq;
using Preesta.Configuration;

namespace Preesta.Data.Supplying
{
    internal class StructureAmbiguitySupplier : IssueSupplier<StructureAmbiguityRule>
    {
        public int MaxIssueCount { get; set; } = 50;

        public StructureAmbiguitySupplier(IJiraService jiraService, IEnumerable<StructureAmbiguityRule> rules, int maxIssueCount = 50)
            : base(jiraService, rules)
        {
            MaxIssueCount = maxIssueCount;
        }

        protected override Issue[] GetIssues(StructureAmbiguityRule rule)
        {
            var enumerator = (from s in rule.Structures
                    from id in JiraService.GetIssuesInStructure(s)
                    group id by id into g
                    where g.Count() > 1
                    select JiraService.GetIssueById(g.Key))
                    ;

            if (MaxIssueCount > 0)
            {
                enumerator = enumerator.Take(MaxIssueCount);
            }

            return enumerator.ToArray();
        }
    }
}
