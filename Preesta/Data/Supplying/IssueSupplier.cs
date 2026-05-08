using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Preesta.Configuration;

namespace Preesta.Data.Supplying
{
    internal abstract class IssueSupplier<TRule> : IPackageSupplier
        where TRule : Rule 
    {
        protected IJiraService JiraService { get; }
        protected IEnumerable<TRule> Rules { get; }

        protected IssueSupplier(IJiraService jiraService, IEnumerable<TRule> rules)
        {
            Rules = rules;
            JiraService = jiraService;
        }

        protected bool IsIssueInAccordanceWithPredicate(string additionalPredicateName, Issue issue)
        {
            return (bool) typeof (ExtendedFilteringPredicates)!
                .GetMethod(additionalPredicateName, BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] {JiraService, issue})!;
        }

        protected string ReplaceMarkersByRealAddresses(string[] metaAddressees, IssueParticipants staff)
        {
            var markedStaff = new Dictionary<string, User?>
                          {
                              {"assignee", staff.Assignee},
                              {"reporter", staff.Reporter},
                              {"creator", staff.Creator}
                          };

            var defaultUser = new User();
            
            string GetUserMail(User? u) => (u ?? defaultUser).Email.ToLower();

            return string.Join(",", metaAddressees
                .Where(m => !markedStaff.ContainsKey(m))
                .Union(metaAddressees
                        .Where(markedStaff.ContainsKey)
                        .Select(m => GetUserMail(markedStaff[m])))
                .Distinct()
                .OrderBy(m => m)
                .ToArray());
        }

        public virtual PackageBase[] GetPackages()
        {
            var uncategorizedSet =
            (
                from rule in Rules
                let issues =
                (
                    from issue in GetIssues(rule)
                    where string.IsNullOrWhiteSpace(rule.AdditionalPredicateName)
                          || IsIssueInAccordanceWithPredicate(rule.AdditionalPredicateName, issue)
                    select issue
                ).ToArray()
                where issues.Any()
                select new
                {
                    rule,
                    issues
                }
            ).ToArray();

            var notificationPackages =
                (
                    from set in uncategorizedSet
                    from issue in set.issues
                    where set.rule.Notification != null
                    group new {issue, set.rule} by new
                    {
                        To = ReplaceMarkersByRealAddresses(set.rule.Notification!.RawRecipients, issue.Participants),
                        Cc = ReplaceMarkersByRealAddresses(set.rule.Notification.RawCc, issue.Participants),
                        set.rule.Notification.Subject,
                        Rule = set.rule
                    }
                    into ag
                    let basePackage = new Package<NotificationReaction, Issue>
                    {
                        Items = ag.Select(a => a.issue).ToArray(),
                        Reaction = new NotificationReaction
                        {
                            Addressees = new Addressees
                            {
                                To = ag.Key.To.Split(','),
                                Cc = ag.Key.Cc.Split(',')
                            },
                            Subject = ag.Key.Subject,
                            Recommendations = ag.First().rule.Notification!.Recommendations,
                            TelegramChatIds = ag.First().rule.Notification!.TelegramChatIds,
                            SlackUserIds = ag.First().rule.Notification!.SlackUserIds,
                            Columns = ag.First().rule.Notification!.Columns
                        }
                    }
                    select Enrich(basePackage, ag.First().rule)
                )
                .ToArray();

            var sets = uncategorizedSet
                .Select(s => (rule: s.rule, issues: s.issues))
                .ToArray();
            var actionPackages = GetMutationPackages(sets);

            return notificationPackages.Union(actionPackages).ToArray();
        }

        /// <summary>
        /// Produce the mutation/action packages for a batch of (rule, issues) pairs.
        /// Default implementation emits one <see cref="Package{TReaction,TItem}"/> of
        /// <see cref="SelfUpdate"/> per <see cref="Rule.Mutations"/> entry — the REST
        /// path used by Jira. Linear-style suppliers override to emit GraphQL packages
        /// from a different rule field.
        /// </summary>
        protected virtual PackageBase[] GetMutationPackages((TRule rule, Issue[] issues)[] sets)
        {
            return
            (
                from set in sets
                from updateAction in set.rule.Mutations
                let package = new Package<SelfUpdate, Issue>
                {
                    Reaction = new SelfUpdate
                    {
                        BodyPattern = updateAction.BodyPattern,
                        UrlPattern = updateAction.UrlPattern,
                        Verb = updateAction.Verb
                    },
                    Items = set.issues
                }
                select package
            )
            .Cast<PackageBase>()
            .ToArray();
        }

        protected internal virtual PackageBase Enrich(PackageBase basePackage, TRule rule)
        {
            return basePackage;
        }

        protected abstract Issue[] GetIssues(TRule rule);
    }
}
