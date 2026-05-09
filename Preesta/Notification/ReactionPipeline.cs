using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Messaging;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;

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
        public ILinearMutationHandler? LinearMutationHandler { get; set; }
        public Redirector Redirector { get; set; } = Redirector.Empty;
        public IReadOnlyDictionary<string, string> TelegramUserMap { get; set; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> SlackUserMap { get; set; } = new Dictionary<string, string>();
        public string LogoFileName { get; set; } = string.Empty;

        public ReactionPipeline(
            IPackageSupplier? packageSupplier = null,
            IPackageConverter<TIssueType>? packageConverter = null,
            IMessenger? messenger = null,
            IHttpHandler? httpHandler = null,
            Redirector? redirector = null,
            string logoFileName = "",
            IMessenger? telegramMessenger = null,
            IReadOnlyDictionary<string, string>? telegramUserMap = null,
            ILinearMutationHandler? linearMutationHandler = null,
            IMessenger? slackMessenger = null,
            IReadOnlyDictionary<string, string>? slackUserMap = null)
        {
            PackageSupplier = packageSupplier;
            PackageConverter = packageConverter;
            Messenger = messenger;
            HttpHandler = httpHandler;
            LinearMutationHandler = linearMutationHandler;
            Redirector = redirector ?? Redirector.Empty;
            LogoFileName = logoFileName;
            TelegramMessenger = telegramMessenger;
            TelegramUserMap = telegramUserMap ?? new Dictionary<string, string>();
            SlackMessenger = slackMessenger;
            SlackUserMap = slackUserMap ?? new Dictionary<string, string>();
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

            // Email
            var emailMessages = notificationPackages
                .ToMessages(PackageConverter)
                .Redirect(Redirector)
                .SetLogo(LogoFileName);

            Messenger?.SendAll(emailMessages);

            // Telegram
            if (TelegramMessenger != null)
            {
                var telegramMessages = notificationPackages
                    .ToTelegramMessages(PackageConverter, Redirector, TelegramUserMap);
                TelegramMessenger.SendAll(telegramMessages);
            }

            // Slack (personal DMs via chat.postMessage)
            if (SlackMessenger != null)
            {
                var slackMessages = notificationPackages
                    .ToSlackMessages(PackageConverter, Redirector, SlackUserMap);
                SlackMessenger.SendAll(slackMessages);
            }

            // Self-updates (REST calls)
            var selfUpdates = allPackages
                    .OfType<Package<SelfUpdate, TIssueType>>()
                    .ToHttpRequests(PackageConverter)
                ;

            HttpHandler?.HandleAll(selfUpdates);

            // GraphQL mutations (Linear)
            var graphQLBodies = allPackages
                    .OfType<Package<GraphQLMutation, TIssueType>>()
                    .ToGraphQLMutationBodies(PackageConverter)
                ;

            LinearMutationHandler?.HandleAll(graphQLBodies);
        }

        public async Task RunAsync()
        {
            await Task.Factory.StartNew(Run);
        }
    }
}
