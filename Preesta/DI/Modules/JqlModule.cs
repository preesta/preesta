using Preesta.AppConfig;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using Preesta.Notification.Mutation;

namespace Preesta.DI.Modules
{
    /// <summary>
    /// Jira (JQL) issue digests. Jira is just another optional source — this
    /// module is configured exactly when a <c>Jira:</c> section is present.
    /// Its supplier reads via <see cref="IJiraService"/> and its write side
    /// is Jira's REST <c>callRest</c>, so the same service plugs into both.
    /// </summary>
    internal sealed class JqlModule : ITrackerModule
    {
        public string Key => "Jql";

        public bool IsConfigured(AppSettings settings) => settings.Jira != null;

        public ReactionPipeline<Issue> BuildPipeline(TrackerBuildContext c)
        {
            // IsConfigured guarantees a Jira service was built for this run.
            var jira = c.JiraService!;
            var supplier = new JqlSupplier(jira, c.Rules.GetJqlRules(c.Group), c.Logger);
            var converter = new IssuePackageConverter(
                c.Settings.Jira!.RootUri, c.Settings.SubjectPrefix, customFields: c.CustomFields);

            return new ReactionPipeline<Issue>
            {
                PackageSupplier = supplier,
                PackageConverter = converter,
                Channels = c.Channels,
                Mutations = new RestMutations(jira),
                Logger = c.Logger
            };
        }
    }
}
