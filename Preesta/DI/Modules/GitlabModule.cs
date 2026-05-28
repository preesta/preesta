using System;
using Preesta.AppConfig;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using Preesta.Notification.Mutation;

namespace Preesta.DI.Modules
{
    internal sealed class GitlabModule : ITrackerModule
    {
        public string Key => "Gitlab";

        public bool IsConfigured(AppSettings settings) =>
            !string.IsNullOrEmpty(settings.GitlabToken);

        public ReactionPipeline<Issue> BuildPipeline(TrackerBuildContext c)
        {
            // GitlabConnection defaults to https://gitlab.com/api/graphql when
            // apiBase is empty; self-hosted instances override it.
            var apiBase = c.Settings.GitlabApiBase;
            var connection = string.IsNullOrEmpty(apiBase)
                ? new GitlabGraphQL.GitlabConnection(c.Settings.GitlabToken!)
                : new GitlabGraphQL.GitlabConnection(c.Settings.GitlabToken!, apiBase!);
            var source = new GitlabIssueSource(connection, c.Logger);
            var supplier = new GitlabIssueSupplier(
                source, c.JiraService, c.Rules.GetGitlabRules(c.Tags), c.Logger);
            var executor = new GitlabMutationExecutor(connection, c.Logger);

            // Issue.Url is populated by the source and preferred by the formatter;
            // the derived root (endpoint minus /api/graphql) is only the fallback
            // host for self-hosted instances.
            var converter = new IssuePackageConverter(
                DeriveRoot(apiBase), c.Settings.SubjectPrefix, customFields: c.CustomFields);

            return new ReactionPipeline<Issue>
            {
                PackageSupplier = supplier,
                PackageConverter = converter,
                Channels = c.Channels,
                Mutations = new GraphQLMutations(executor),
                Logger = c.Logger
            };
        }

        /// <summary>
        /// Strips <c>/api/graphql</c> off the configured endpoint so the fallback
        /// "Open in GitLab →" link points at the right host on self-hosted
        /// instances. Returns <c>https://gitlab.com/</c> when nothing is set.
        /// </summary>
        private static string DeriveRoot(string? apiBase)
        {
            if (string.IsNullOrEmpty(apiBase)) return "https://gitlab.com/";
            try
            {
                var uri = new Uri(apiBase);
                return $"{uri.Scheme}://{uri.Authority}/";
            }
            catch
            {
                return "https://gitlab.com/";
            }
        }
    }
}
