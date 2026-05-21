using Preesta.AppConfig;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using Preesta.Notification.Mutation;

namespace Preesta.DI.Modules
{
    internal sealed class GithubModule : ITrackerModule
    {
        public string Key => "Github";

        public bool IsConfigured(AppSettings settings) =>
            !string.IsNullOrEmpty(settings.GithubToken);

        public ReactionPipeline<Issue> BuildPipeline(TrackerBuildContext c)
        {
            var connection = new GithubGraphQL.GithubConnection(c.Settings.GithubToken!);
            var source = new GithubIssueSource(connection, c.Logger);
            var supplier = new GithubIssueSupplier(
                source, c.JiraService, c.Rules.GetGithubRules(c.Group), c.Logger);
            var executor = new GithubMutationExecutor(connection, c.Logger);

            // Issue.Url is populated by the source and preferred by the formatter;
            // the root here is only a fallback.
            var converter = new IssuePackageConverter(
                "https://github.com/", c.Settings.SubjectPrefix, customFields: c.CustomFields);

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
