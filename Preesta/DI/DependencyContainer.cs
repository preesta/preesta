using System;
using System.IO;
using System.Xml.Linq;
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
                ? new HttpJiraService(appSettings.JiraRootUri, appSettings.ApiToken, appSettings.MaxResults)
                : new HttpJiraService(appSettings.JiraRootUri, appSettings.UserName, appSettings.Password, appSettings.MaxResults);

            var messenger = new SmtpClient(appSettings.SmtpSection);

            IMessenger? telegramMessenger = null;
            var telegramToken = appSettings.TelegramBotToken;
            if (!string.IsNullOrEmpty(telegramToken))
                telegramMessenger = new TelegramMessenger(telegramToken);

            var rulesFileName = appSettings.LocalRulesFileName;
            var rulesConfig = CreateRulesConfig(rulesFileName, logger);

            var jqlSupplier = new JqlSupplier(jiraService, rulesConfig.GetJqlRules(@group), logger);
            var structSupplier = new StructureAmbiguitySupplier(
                jiraService, rulesConfig.GetStructureAmbiguityRules(@group), appSettings.MaxResults);
            var buildSupplier = new ReleaseSupplier(jiraService, rulesConfig.GetReleaseRules(@group));

            var issueConverter = new IssuePackageConverter(appSettings.JiraRootUri, appSettings.SubjectPrefix);
            var buildConverter = new ReleasePackageConverter(appSettings.SubjectPrefix);

            var redirector = new Redirector(
                rulesConfig.GetRedirectionMap(), appSettings.Supervisors, appSettings.Maintainers);
            var telegramUserMap = rulesConfig.GetTelegramUserMap();

            var logoFileName = appSettings.LogoFileName;

            var services = new ServiceCollection();
            services.AddSingleton<IRulesConfig>(rulesConfig);

            services.AddKeyedSingleton("Jql", new ReactionPipeline<Issue>(
                jqlSupplier, issueConverter, messenger, jiraService, redirector, logoFileName, telegramMessenger, telegramUserMap));

            services.AddKeyedSingleton("Structure", new ReactionPipeline<Issue>(
                structSupplier, issueConverter, messenger, jiraService, redirector, logoFileName, telegramMessenger, telegramUserMap));

            services.AddSingleton(new ReactionPipeline<Release>(
                buildSupplier, buildConverter, messenger, jiraService, redirector, logoFileName, telegramMessenger, telegramUserMap));

            _provider = services.BuildServiceProvider();
        }

        public ReactionPipeline<TIssueType> ResolveNotificationPipe<TIssueType>(string? name = null)
        {
            return name != null
                ? _provider.GetRequiredKeyedService<ReactionPipeline<TIssueType>>(name)
                : _provider.GetRequiredService<ReactionPipeline<TIssueType>>();
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
    }
}
