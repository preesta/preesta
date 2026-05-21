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

            var jiraService = !string.IsNullOrEmpty(appSettings.ApiToken)
                ? new HttpJiraService(appSettings.JiraRootUri, appSettings.ApiToken, appSettings.MaxResults, logger: logger)
                : new HttpJiraService(appSettings.JiraRootUri, appSettings.UserName, appSettings.Password, appSettings.MaxResults, logger: logger);

            // Discover custom fields once at startup. On failure (HTTP error, no permissions),
            // HttpJiraService logs a warning and returns an empty map — custom-field columns
            // referenced in rules.yaml will then render as empty, but nothing crashes.
            var customFields = jiraService.GetCustomFieldMap();

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

            var jqlSupplier = new JqlSupplier(jiraService, rulesConfig.GetJqlRules(@group), logger);
            var buildSupplier = new ReleaseSupplier(jiraService, rulesConfig.GetReleaseRules(@group));

            var issueConverter = new IssuePackageConverter(
                appSettings.JiraRootUri, appSettings.SubjectPrefix, customFields: customFields);
            var buildConverter = new ReleasePackageConverter(appSettings.SubjectPrefix);

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

            services.AddKeyedSingleton("Jql", new ReactionPipeline<Issue>
            {
                PackageSupplier = jqlSupplier,
                PackageConverter = issueConverter,
                Channels = channels,
                Mutations = new RestMutations(jiraService),
                Logger = logger
            });

            services.AddSingleton(new ReactionPipeline<Release>
            {
                PackageSupplier = buildSupplier,
                PackageConverter = buildConverter,
                Channels = channels,
                Mutations = new RestMutations(jiraService),
                Logger = logger
            });

            // Optional issue trackers are self-contained modules. Each knows
            // whether it's configured and how to build its own pipeline; the
            // orchestrator just registers the configured ones under their key.
            // Adding a tracker is one new ITrackerModule plus one list entry —
            // nothing here changes.
            var trackerContext = new TrackerBuildContext(
                appSettings, rulesConfig, @group, channels, customFields, jiraService, logger);
            var modules = new ITrackerModule[]
            {
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
