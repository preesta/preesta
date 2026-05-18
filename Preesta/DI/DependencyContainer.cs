using System;
using System.IO;
using System.Xml.Linq;
using LinearGraphQL;
using Microsoft.Extensions.DependencyInjection;
using PlaneRest;
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

            // Plane pipeline — REST tracker, registered only when an API key and
            // workspace slug are both configured (one identity at appsettings level,
            // many rules in rules.yaml each scoped to a Plane project via projectId).
            if (!string.IsNullOrEmpty(appSettings.PlaneApiKey)
                && !string.IsNullOrEmpty(appSettings.PlaneWorkspaceSlug))
            {
                var planeApiBase = string.IsNullOrEmpty(appSettings.PlaneApiBase)
                    ? PlaneConnection.DefaultApiBase
                    : appSettings.PlaneApiBase!;
                var planeConnection = new PlaneConnection(
                    appSettings.PlaneApiKey!, appSettings.PlaneWorkspaceSlug!, planeApiBase);
                var planeSource = new PlaneIssueSource(planeConnection, logger);
                var planeSupplier = new PlaneIssueSupplier(
                    planeSource, jiraService, rulesConfig.GetPlaneRules(@group), logger);
                var planeMutationExecutor = new PlaneMutationExecutor(planeConnection, logger);

                // For Plane-sourced issues, Issue.Url is null (Plane's API doesn't return
                // a browse URL). Pass the workspace web root as rootUri so the formatter
                // builds /<slug>/projects/<projectId>/issues/<sequence_id> links via the
                // PlaneIssueUriOrNull helper.
                var planeWebRoot = ResolvePlaneWebRoot(planeApiBase);
                var planeConverter = new IssuePackageConverter(
                    planeWebRoot, appSettings.SubjectPrefix, customFields: customFields);

                services.AddKeyedSingleton("Plane", new ReactionPipeline<Issue>(
                    planeSupplier, planeConverter, messenger, planeMutationExecutor, redirector,
                    logoFileName, telegramMessenger, telegramUserMap,
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
        /// Derives the user-facing web app root from Plane's API base. Plane Cloud
        /// uses <c>api.plane.so</c> for API and <c>app.plane.so</c> for the UI;
        /// self-hosted instances commonly serve both from the same host. We try
        /// the api→app substitution first (cloud convention); when that doesn't
        /// apply we fall back to the API base, which works for self-hosted setups
        /// where /workspaces/... is also the UI path.
        /// </summary>
        private static string ResolvePlaneWebRoot(string apiBase)
        {
            if (apiBase.Contains("api.plane.so", StringComparison.OrdinalIgnoreCase))
                return apiBase.Replace("api.plane.so", "app.plane.so", StringComparison.OrdinalIgnoreCase);
            return apiBase;
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
