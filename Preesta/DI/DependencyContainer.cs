using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Messaging;
using Preesta.AppConfig;
using Preesta.Configuration;
using Preesta.Data;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.DI.Modules;
using Preesta.Notification;
using Preesta.Notification.Delivery;
using Preesta.Notification.Mutation;
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

            // Jira is one of five equal Sources — present only when configured.
            // Linear-/GitHub-/GitLab-/Shortcut-only deployments leave it null.
            var jiraService = CreateJiraService(appSettings.Jira, logger);

            // Discover custom fields once at startup when Jira is wired in. On
            // failure (HTTP error, no permissions) HttpJiraService logs a warning
            // and returns an empty map; without Jira the map is simply empty —
            // custom-field columns then render as empty cells, never crash.
            IReadOnlyDictionary<string, string> customFields =
                jiraService?.GetCustomFieldMap()
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Each delivery channel is independent — any subset may be configured.
            IMessenger? messenger = appSettings.Smtp is not null
                ? new SmtpClient(appSettings.Smtp)
                : null;

            IMessenger? telegramMessenger = null;
            var telegramToken = appSettings.TelegramBotToken;
            if (!string.IsNullOrEmpty(telegramToken))
                telegramMessenger = new TelegramMessenger(telegramToken, logger: logger);

            IMessenger? slackMessenger = null;
            var slackToken = appSettings.SlackBotToken;
            if (!string.IsNullOrEmpty(slackToken))
                slackMessenger = new SlackMessenger(slackToken, logger: logger);

            var rulesFileName = appSettings.LocalRulesFileName;
            var rulesConfig = CreateRulesConfig(rulesFileName, logger);

            var redirector = new Redirector(
                rulesConfig.GetRedirectionMap(), appSettings.Supervisors, appSettings.Maintainers);
            var telegramUserMap = rulesConfig.GetTelegramUserMap();
            var slackUserMap = rulesConfig.GetSlackUserMap();

            WarnOnEmailOnlyRecipientsWithoutSmtp(
                smtpConfigured: messenger != null,
                rulesConfig, @group, slackUserMap, telegramUserMap, logger);

            // Assemble the delivery targets once — every pipeline shares them.
            var channels = DeliveryChannels.Build(
                email: messenger,
                telegram: telegramMessenger, telegramUsers: telegramUserMap,
                slack: slackMessenger, slackUsers: slackUserMap,
                redirector: redirector,
                logoFileName: appSettings.LogoFileName);

            var services = new ServiceCollection();
            services.AddSingleton<IRulesConfig>(rulesConfig);

            // Every issue tracker — Jira included — is a self-contained module.
            // Each knows whether it's configured and how to build its own
            // pipeline; the orchestrator just registers the configured ones
            // under their key. Adding a tracker is one new ITrackerModule plus
            // one list entry — nothing here changes.
            var trackerContext = new TrackerBuildContext(
                appSettings, rulesConfig, @group, channels, customFields, jiraService, logger);
            var modules = new ITrackerModule[]
            {
                new JqlModule(),
                new LinearModule(),
                new GitlabModule(),
                new GithubModule(),
                new ShortcutModule(),
            };
            foreach (var module in modules)
            {
                if (!module.IsConfigured(appSettings)) continue;
                services.AddKeyedSingleton(module.Key, module.BuildPipeline(trackerContext));
            }

            // Jira release/version digests are Jira-bound; register only when
            // Jira is configured. (Release is a separate item type, so it isn't
            // an ITrackerModule<Issue>.)
            if (jiraService != null)
            {
                services.AddSingleton(new ReactionPipeline<Release>
                {
                    PackageSupplier = new ReleaseSupplier(jiraService, rulesConfig.GetReleaseRules(@group)),
                    PackageConverter = new ReleasePackageConverter(appSettings.SubjectPrefix),
                    Channels = channels,
                    Mutations = new RestMutations(jiraService),
                    Logger = logger
                });
            }

            _provider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Returns the pipeline if it was registered (keyed by <paramref name="name"/>
        /// when supplied, otherwise the unkeyed singleton), or <c>null</c> when its
        /// underlying tracker isn't configured.
        /// </summary>
        public ReactionPipeline<TIssueType>? TryResolveNotificationPipe<TIssueType>(string? name = null)
        {
            return name != null
                ? _provider.GetKeyedService<ReactionPipeline<TIssueType>>(name)
                : _provider.GetService<ReactionPipeline<TIssueType>>();
        }

        internal void ValidateRules()
        {
            _provider.GetRequiredService<IRulesConfig>().ValidateSchema();
        }

        /// <summary>
        /// Builds the Jira service from config, or returns null when Jira isn't
        /// configured. Picks Bearer (apiToken) over Basic (userName+password)
        /// — JiraConfigLoader guarantees one of the two is present.
        /// </summary>
        private static HttpJiraService? CreateJiraService(JiraConfig? jira, ILogger logger)
        {
            if (jira == null) return null;
            return jira.ApiToken != null
                ? new HttpJiraService(jira.RootUri, jira.ApiToken, jira.MaxResults, logger: logger)
                : new HttpJiraService(jira.RootUri, jira.UserName!, jira.Password!, jira.MaxResults, logger: logger);
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

        /// <summary>
        /// When SMTP is not configured, literal-email recipients in <c>mailTo:</c> / <c>cc:</c>
        /// have no delivery path unless the same address is also listed in the
        /// <c>telegramUsers:</c> or <c>slackUsers:</c> maps. This walks every rule for the
        /// active group and logs one warning per orphaned address so a silently-missed
        /// recipient doesn't go unnoticed.
        /// </summary>
        private static void WarnOnEmailOnlyRecipientsWithoutSmtp(
            bool smtpConfigured,
            IRulesConfig rulesConfig,
            string group,
            IReadOnlyDictionary<string, string> slackUserMap,
            IReadOnlyDictionary<string, string> telegramUserMap,
            ILogger logger)
        {
            if (smtpConfigured) return;

            var allRules = new IEnumerable<Configuration.Rule>[]
            {
                rulesConfig.GetJqlRules(group),
                rulesConfig.GetReleaseRules(group),
                rulesConfig.GetLinearRules(group),
                rulesConfig.GetGitlabRules(group),
                rulesConfig.GetGithubRules(group),
                rulesConfig.GetShortcutRules(group),
            }.SelectMany(r => r);

            var slack = new HashSet<string>(slackUserMap.Keys, StringComparer.OrdinalIgnoreCase);
            var telegram = new HashSet<string>(telegramUserMap.Keys, StringComparer.OrdinalIgnoreCase);

            var orphaned = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in allRules)
            {
                var spec = rule.Notification;
                if (spec == null) continue;
                foreach (var recipient in spec.RawRecipients.Concat(spec.RawCc))
                {
                    var trimmed = recipient.Trim();
                    // Markers like "assignee" / "reporter" don't contain '@' — skip; the
                    // warning is specifically about literal email addresses that have no
                    // Slack/Telegram counterpart and would just be dropped.
                    if (!trimmed.Contains('@')) continue;
                    if (slack.Contains(trimmed) || telegram.Contains(trimmed)) continue;
                    orphaned.Add(trimmed);
                }
            }

            if (orphaned.Count == 0) return;

            logger.Warning(
                "SMTP is not configured, but {Count} email recipient(s) in rules have no " +
                "Slack/Telegram fallback and will receive nothing: {Emails}",
                orphaned.Count, string.Join(", ", orphaned));
        }
    }
}
