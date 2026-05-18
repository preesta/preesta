using System;
using System.IO;
using System.Xml.Linq;
using LinearGraphQL;
using Microsoft.Extensions.DependencyInjection;
using Messaging;
using Preesta.AppConfig;
using Preesta.Configuration;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;
using Serilog;

namespace Preesta.DI
{
    internal class DependencyContainer
    {
        private readonly ServiceProvider _provider;

        public DependencyContainer(string @group)
        {
            var appSettings = new AppSettings();
            appSettings.Validate();

            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(appSettings.LoggerSection)
                .CreateLogger();

            var jiraService = !string.IsNullOrEmpty(appSettings.ApiToken)
                ? new HttpJiraService(appSettings.JiraRootUri, appSettings.ApiToken, appSettings.MaxResults, logger: logger)
                : new HttpJiraService(appSettings.JiraRootUri, appSettings.UserName, appSettings.Password, appSettings.MaxResults, logger: logger);

            // Discover custom fields once at startup. On failure (HTTP error, no permissions),
            // HttpJiraService logs a warning and returns an empty map — custom-field columns
            // referenced in rules.yaml will then render as empty, but nothing crashes.
            var customFields = jiraService.GetCustomFieldMap();

            var messenger = new SmtpClient(appSettings.SmtpSection);

            IMessenger? telegramMessenger = null;
            var telegramToken = appSettings.TelegramBotToken;
            if (!string.IsNullOrEmpty(telegramToken))
                telegramMessenger = new TelegramMessenger(telegramToken);

            IMessenger? slackMessenger = null;
            var slackToken = appSettings.SlackBotToken;
            if (!string.IsNullOrEmpty(slackToken))
                slackMessenger = new SlackMessenger(slackToken, logger: logger);

            var rulesFileName = appSettings.LocalRulesFileName;
            var rulesConfig = CreateRulesConfig(rulesFileName, logger);

            var jqlSupplier = new JqlSupplier(jiraService, rulesConfig.GetJqlRules(@group), logger);
            var buildSupplier = new ReleaseSupplier(jiraService, rulesConfig.GetReleaseRules(@group));

            var issueConverter = new IssuePackageConverter(
                appSettings.JiraRootUri, appSettings.SubjectPrefix, customFields: customFields);
            var buildConverter = new ReleasePackageConverter(appSettings.SubjectPrefix);

            var redirector = new Redirector(
                rulesConfig.GetRedirectionMap(), appSettings.Supervisors, appSettings.Maintainers);
            var telegramUserMap = rulesConfig.GetTelegramUserMap();
            var slackUserMap = rulesConfig.GetSlackUserMap();

            var logoFileName = appSettings.LogoFileName;

            var services = new ServiceCollection();
            services.AddSingleton<IRulesConfig>(rulesConfig);

            services.AddKeyedSingleton("Jql", new ReactionPipeline<Issue>(
                jqlSupplier, issueConverter, messenger, jiraService, redirector, logoFileName,
                telegramMessenger, telegramUserMap,
                slackMessenger: slackMessenger, slackUserMap: slackUserMap));

            services.AddSingleton(new ReactionPipeline<Release>(
                buildSupplier, buildConverter, messenger, jiraService, redirector, logoFileName,
                telegramMessenger, telegramUserMap,
                slackMessenger: slackMessenger, slackUserMap: slackUserMap));

            // Linear pipeline is registered only when an API key is provided.
            // Application.cs uses GetKeyedService (nullable) so the pipeline is
            // skipped silently when Linear isn't configured.
            if (!string.IsNullOrEmpty(appSettings.LinearApiKey))
            {
                // One LinearConnection (= ILinearGateway) shared between the read path
                // (LinearIssueSource) and the write path (LinearMutationExecutor) — same
                // HttpClient, same auth header, no duplicate setup.
                var linearConnection = new LinearConnection(appSettings.LinearApiKey!);
                var linearSource = new LinearIssueSource(linearConnection, logger);
                var linearSupplier = new LinearIssueSupplier(
                    linearSource, jiraService, rulesConfig.GetLinearRules(@group), logger);
                var linearMutationExecutor = new LinearMutationExecutor(linearConnection, logger);

                // IssuePackageConverter is reused as-is. For Linear issues, Issue.Url is
                // populated by LinearIssueSource and the formatter prefers it over the
                // reconstructed-from-rootUri form, so the rootUri passed here is only
                // a fallback (and a workspace-scoped URL works either way).
                var linearWorkspace = appSettings.LinearWorkspace;
                var linearRootUri = string.IsNullOrEmpty(linearWorkspace)
                    ? "https://linear.app/"
                    : $"https://linear.app/{linearWorkspace}/";
                // Pass Jira's customFields map through too — Linear-sourced issues
                // have empty Issue.CustomFields, so the map is effectively unused,
                // but keeps construction symmetric for future reuse.
                var linearConverter = new IssuePackageConverter(
                    linearRootUri, appSettings.SubjectPrefix, linearWorkspace, customFields);

                services.AddKeyedSingleton("Linear", new ReactionPipeline<Issue>(
                    linearSupplier, linearConverter, messenger, jiraService, redirector,
                    logoFileName, telegramMessenger, telegramUserMap, linearMutationExecutor,
                    slackMessenger: slackMessenger, slackUserMap: slackUserMap));
            }

            // GitLab pipeline mirrors GitHub — registered only when a token is provided.
            if (!string.IsNullOrEmpty(appSettings.GitlabToken))
            {
                // GitlabConnection picks the default https://gitlab.com/api/graphql
                // endpoint when apiBase is empty; self-hosted instances override it.
                var gitlabApiBase = appSettings.GitlabApiBase;
                var gitlabConnection = string.IsNullOrEmpty(gitlabApiBase)
                    ? new GitlabGraphQL.GitlabConnection(appSettings.GitlabToken!)
                    : new GitlabGraphQL.GitlabConnection(appSettings.GitlabToken!, gitlabApiBase!);
                var gitlabSource = new GitlabIssueSource(gitlabConnection, logger);
                var gitlabSupplier = new GitlabIssueSupplier(
                    gitlabSource, jiraService, rulesConfig.GetGitlabRules(@group), logger);
                var gitlabMutationExecutor = new GitlabMutationExecutor(gitlabConnection, logger);

                // For GitLab-sourced issues, Issue.Url is populated by GitlabIssueSource
                // and the formatter prefers it — rootUri is a fallback only. We derive
                // a workspace-style root from the configured endpoint (strip /api/graphql)
                // so the fallback URL still points to the right host for self-hosted.
                var gitlabRootUri = DeriveGitlabRoot(gitlabApiBase);
                var gitlabConverter = new IssuePackageConverter(
                    gitlabRootUri, appSettings.SubjectPrefix, customFields: customFields);

                services.AddKeyedSingleton("Gitlab", new ReactionPipeline<Issue>(
                    gitlabSupplier, gitlabConverter, messenger, jiraService, redirector,
                    logoFileName, telegramMessenger, telegramUserMap, gitlabMutationExecutor,
                    slackMessenger: slackMessenger, slackUserMap: slackUserMap));
            }

            // GitHub pipeline mirrors Linear — registered only when a token is provided.
            if (!string.IsNullOrEmpty(appSettings.GithubToken))
            {
                var githubConnection = new GithubGraphQL.GithubConnection(appSettings.GithubToken!);
                var githubSource = new GithubIssueSource(githubConnection, logger);
                var githubSupplier = new GithubIssueSupplier(
                    githubSource, jiraService, rulesConfig.GetGithubRules(@group), logger);
                var githubMutationExecutor = new GithubMutationExecutor(githubConnection, logger);

                // For GitHub-sourced issues, Issue.Url is populated by GithubIssueSource and
                // the formatter prefers it — rootUri is a fallback only.
                var githubConverter = new IssuePackageConverter(
                    "https://github.com/", appSettings.SubjectPrefix, customFields: customFields);

                services.AddKeyedSingleton("Github", new ReactionPipeline<Issue>(
                    githubSupplier, githubConverter, messenger, jiraService, redirector,
                    logoFileName, telegramMessenger, telegramUserMap, githubMutationExecutor,
                    slackMessenger: slackMessenger, slackUserMap: slackUserMap));
            }

            // Shortcut pipeline mirrors GitHub — registered only when an API token is provided.
            // Shortcut is REST-only (no GraphQL), so the mutation executor plugs into the
            // same IHttpHandler slot as Jira (not the IGraphQLMutationHandler slot).
            if (!string.IsNullOrEmpty(appSettings.ShortcutApiToken))
            {
                var shortcutConnection = new ShortcutRest.ShortcutConnection(appSettings.ShortcutApiToken!);
                var shortcutSource = new ShortcutIssueSource(shortcutConnection, logger);
                var shortcutSupplier = new ShortcutIssueSupplier(
                    shortcutSource, jiraService, rulesConfig.GetShortcutRules(@group), logger);
                var shortcutMutationExecutor = new ShortcutMutationExecutor(shortcutConnection, logger);

                // Issue.Url is populated by ShortcutIssueSource (app_url); rootUri is a
                // fallback only. The base URL also lets {{@jiraRoot}} marker resolve to
                // the Shortcut API root for rule-supplied mutation urlPattern templates.
                var shortcutConverter = new IssuePackageConverter(
                    "https://api.app.shortcut.com/", appSettings.SubjectPrefix, customFields: customFields);

                services.AddKeyedSingleton("Shortcut", new ReactionPipeline<Issue>(
                    shortcutSupplier, shortcutConverter, messenger, shortcutMutationExecutor, redirector,
                    logoFileName, telegramMessenger, telegramUserMap,
                    slackMessenger: slackMessenger, slackUserMap: slackUserMap));
            }

            _provider = services.BuildServiceProvider();
        }

        public ReactionPipeline<TIssueType> ResolveNotificationPipe<TIssueType>(string? name = null)
        {
            return name != null
                ? _provider.GetRequiredKeyedService<ReactionPipeline<TIssueType>>(name)
                : _provider.GetRequiredService<ReactionPipeline<TIssueType>>();
        }

        /// <summary>
        /// Returns the keyed pipeline if it was registered (e.g. only when its
        /// underlying service is configured), or <c>null</c> otherwise.
        /// </summary>
        public ReactionPipeline<TIssueType>? TryResolveNotificationPipe<TIssueType>(string name)
        {
            return _provider.GetKeyedService<ReactionPipeline<TIssueType>>(name);
        }

        internal void ValidateRules()
        {
            _provider.GetRequiredService<IRulesConfig>().ValidateSchema();
        }

        /// <summary>
        /// Strips the <c>/api/graphql</c> suffix off the configured endpoint so the
        /// formatter has a usable host root for the fallback "Open in GitLab →" link
        /// on self-hosted instances. Returns <c>https://gitlab.com/</c> when nothing
        /// is configured.
        /// </summary>
        private static string DeriveGitlabRoot(string? apiBase)
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

        private static IRulesConfig CreateRulesConfig(string path, ILogger logger)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".yaml" or ".yml" => YamlRulesConfig.FromFile(path, logger),
                _ => new XmlRulesConfig(XDocument.Load(path), logger)
            };
        }
    }
}
