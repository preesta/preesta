using LinearGraphQL;
using Preesta.AppConfig;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using Preesta.Notification.Mutation;

namespace Preesta.DI.Modules
{
    internal sealed class LinearModule : ITrackerModule
    {
        public string Key => "Linear";

        public bool IsConfigured(AppSettings settings) =>
            !string.IsNullOrEmpty(settings.LinearApiKey);

        public ReactionPipeline<Issue> BuildPipeline(TrackerBuildContext c)
        {
            // One LinearConnection (= ILinearGateway) shared between the read path
            // (LinearIssueSource) and the write path (LinearMutationExecutor) — same
            // HttpClient, same auth header, no duplicate setup.
            var connection = new LinearConnection(c.Settings.LinearApiKey!);
            var source = new LinearIssueSource(connection, c.Logger);
            var supplier = new LinearIssueSupplier(
                source, c.JiraService, c.Rules.GetLinearRules(c.Group), c.Logger);
            var executor = new LinearMutationExecutor(connection, c.Logger);

            // For Linear issues, Issue.Url is populated by the source and the
            // formatter prefers it, so the workspace-scoped root here is only a
            // fallback. CustomFields is effectively unused (Linear issues have no
            // flat custom-field map) but kept for construction symmetry.
            var workspace = c.Settings.LinearWorkspace;
            var rootUri = string.IsNullOrEmpty(workspace)
                ? "https://linear.app/"
                : $"https://linear.app/{workspace}/";
            var converter = new IssuePackageConverter(
                rootUri, c.Settings.SubjectPrefix, workspace, c.CustomFields);

            return new ReactionPipeline<Issue>
            {
                PackageSupplier = supplier,
                PackageConverter = converter,
                Channels = c.Channels,
                Mutations = new GraphQLMutations(executor),
                Logger = c.Logger
            };
        }
    }
}
