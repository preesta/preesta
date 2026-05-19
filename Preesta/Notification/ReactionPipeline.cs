using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Messaging;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;
using Serilog;

namespace Preesta.Notification
{
    internal class ReactionPipeline<TIssueType>
    {
        public IPackageSupplier? PackageSupplier { get; set; }
        public IPackageConverter<TIssueType>? PackageConverter { get; set; }
        public IMessenger? Messenger { get; set; }
        public IMessenger? TelegramMessenger { get; set; }
        public IMessenger? SlackMessenger { get; set; }
        public IHttpHandler? HttpHandler { get; set; }
        public IGraphQLMutationHandler? GraphQLMutationHandler { get; set; }
        public Redirector Redirector { get; set; } = Redirector.Empty;
        public IReadOnlyDictionary<string, string> TelegramUserMap { get; set; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> SlackUserMap { get; set; } = new Dictionary<string, string>();
        public string LogoFileName { get; set; } = string.Empty;
        public ILogger? Logger { get; set; }

        public ReactionPipeline(
            IPackageSupplier? packageSupplier = null,
            IPackageConverter<TIssueType>? packageConverter = null,
            IMessenger? messenger = null,
            IHttpHandler? httpHandler = null,
            Redirector? redirector = null,
            string logoFileName = "",
            IMessenger? telegramMessenger = null,
            IReadOnlyDictionary<string, string>? telegramUserMap = null,
            IGraphQLMutationHandler? graphQLMutationHandler = null,
            IMessenger? slackMessenger = null,
            IReadOnlyDictionary<string, string>? slackUserMap = null,
            ILogger? logger = null)
        {
            PackageSupplier = packageSupplier;
            PackageConverter = packageConverter;
            Messenger = messenger;
            HttpHandler = httpHandler;
            GraphQLMutationHandler = graphQLMutationHandler;
            Redirector = redirector ?? Redirector.Empty;
            LogoFileName = logoFileName;
            TelegramMessenger = telegramMessenger;
            TelegramUserMap = telegramUserMap ?? new Dictionary<string, string>();
            SlackMessenger = slackMessenger;
            SlackUserMap = slackUserMap ?? new Dictionary<string, string>();
            Logger = logger;
        }

        public void Run()
        {
            if (PackageConverter == null || PackageSupplier == null)
            {
                return;
            }

            var allPackages = PackageSupplier.GetPackages();

            var notificationPackages = allPackages
                .OfType<Package<NotificationReaction, TIssueType>>()
                .ToArray();

            // Each channel runs inside its own try/catch — a misconfigured or
            // unreachable channel (bad SMTP creds, blocked Telegram user, expired
            // Slack token) must not abort the rest of the run. Failures are logged
            // at Error and the next channel proceeds.

            TryRunStage("email send", () =>
            {
                var emailMessages = notificationPackages
                    .ToMessages(PackageConverter)
                    .Redirect(Redirector)
                    .SetLogo(LogoFileName);
                Messenger?.SendAll(emailMessages);
            });

            TryRunStage("telegram send", () =>
            {
                if (TelegramMessenger != null)
                {
                    var telegramMessages = notificationPackages
                        .ToTelegramMessages(PackageConverter, Redirector, TelegramUserMap);
                    TelegramMessenger.SendAll(telegramMessages);
                }
            });

            TryRunStage("slack send", () =>
            {
                if (SlackMessenger != null)
                {
                    var slackMessages = notificationPackages
                        .ToSlackMessages(PackageConverter, Redirector, SlackUserMap);
                    SlackMessenger.SendAll(slackMessages);
                }
            });

            TryRunStage("REST mutations", () =>
            {
                var selfUpdates = allPackages
                    .OfType<Package<SelfUpdate, TIssueType>>()
                    .ToHttpRequests(PackageConverter);
                HttpHandler?.HandleAll(selfUpdates);
            });

            TryRunStage("GraphQL mutations", () =>
            {
                var graphQLBodies = allPackages
                    .OfType<Package<GraphQLMutation, TIssueType>>()
                    .ToGraphQLMutationBodies(PackageConverter);
                GraphQLMutationHandler?.HandleAll(graphQLBodies);
            });
        }

        private void TryRunStage(string stageName, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Pipeline stage '{Stage}' failed; subsequent stages continue", stageName);
            }
        }

        public async Task RunAsync()
        {
            await Task.Factory.StartNew(Run);
        }
    }
}
