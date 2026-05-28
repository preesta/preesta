using Preesta.AppConfig;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using Preesta.Notification.Mutation;

namespace Preesta.DI.Modules
{
    internal sealed class ShortcutModule : ITrackerModule
    {
        public string Key => "Shortcut";

        public bool IsConfigured(AppSettings settings) =>
            !string.IsNullOrEmpty(settings.ShortcutApiToken);

        public ReactionPipeline<Issue> BuildPipeline(TrackerBuildContext c)
        {
            var connection = new ShortcutRest.ShortcutConnection(c.Settings.ShortcutApiToken!);
            var source = new ShortcutIssueSource(connection, c.Logger);
            var supplier = new ShortcutIssueSupplier(
                source, c.JiraService, c.Rules.GetShortcutRules(c.Tags), c.Logger);
            var executor = new ShortcutMutationExecutor(connection, c.Logger);

            // Issue.Url is populated by the source (app_url); the API root here is
            // a fallback and also resolves {{@jiraRoot}} in mutation urlPattern
            // templates. Shortcut is REST-only, so its executor is a RestMutations.
            var converter = new IssuePackageConverter(
                "https://api.app.shortcut.com/", c.Settings.SubjectPrefix, customFields: c.CustomFields);

            return new ReactionPipeline<Issue>
            {
                PackageSupplier = supplier,
                PackageConverter = converter,
                Channels = c.Channels,
                Mutations = new RestMutations(executor),
                Logger = c.Logger
            };
        }
    }
}
