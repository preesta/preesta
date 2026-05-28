using System.Collections.Generic;
using System.Linq;
using Preesta.Configuration;

namespace Preesta.Data.Supplying
{
    internal class ReleaseSupplier : IPackageSupplier
    {
        protected IJiraService JiraService { get; }
        protected IEnumerable<ReleaseRule> Rules { get; }

        public ReleaseSupplier(IJiraService jiraService, IEnumerable<ReleaseRule> rules)
        {
            JiraService = jiraService;
            Rules = rules;
        }

        public PackageBase[] GetPackages()
        {
            string ToOrderedString(IEnumerable<string> a) => string.Join(",", a.OrderBy(c => c).ToArray());
            return (from rule in Rules
                    from build in JiraService.GetReleases(rule.ProjectCode)
                    where rule.IsMatch(build)
                    group new {build, rule} by new
                    {
                        To = ToOrderedString(rule.Notification!.RawRecipients),
                        Cc = ToOrderedString(rule.Notification.RawCc),
                        rule.Notification.Subject
                    }
                    into ag
                    select new Package<NotificationReaction, Release>
                    {
                        Items = ag.Select(a => a.build).ToArray(),
                        Reaction = new NotificationReaction
                        {
                            Addressees = new Addressees
                            {
                                To = ag.Key.To.Split(','),
                                Cc = ag.Key.Cc.Split(',')
                            },
                            Subject = ag.Key.Subject,
                            Followup = ag.First().rule.Notification!.Followup
                        }
                    })
                .Cast<PackageBase>()
                .ToArray();
        }
    }
}