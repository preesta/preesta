using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Messaging;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;

namespace Preesta.Notification
{
    internal class ReactionPipe<TIssueType>
    {
        public IPackageSupplier? PackageSupplier { get; set; }
        public IPackageConverter<TIssueType>? PackageConverter { get; set; }
        public IMessenger? Messenger { get; set; }
        public IMessenger? TelegramMessenger { get; set; }
        public IHttpHandler? HttpHandler { get; set; }
        public Redirector Redirector { get; set; } = Redirector.Empty;
        public IReadOnlyDictionary<string, string> TelegramUserMap { get; set; } = new Dictionary<string, string>();
        public string LogoFileName { get; set; } = string.Empty;

        public ReactionPipe(
            IPackageSupplier? packageSupplier = null,
            IPackageConverter<TIssueType>? packageConverter = null,
            IMessenger? messenger = null,
            IHttpHandler? httpHandler = null,
            Redirector? redirector = null,
            string logoFileName = "",
            IMessenger? telegramMessenger = null,
            IReadOnlyDictionary<string, string>? telegramUserMap = null)
        {
            PackageSupplier = packageSupplier;
            PackageConverter = packageConverter;
            Messenger = messenger;
            HttpHandler = httpHandler;
            Redirector = redirector ?? Redirector.Empty;
            LogoFileName = logoFileName;
            TelegramMessenger = telegramMessenger;
            TelegramUserMap = telegramUserMap ?? new Dictionary<string, string>();
        }

        public void Run()
        {
            if (PackageConverter == null || PackageSupplier == null)
            {
                return;
            }

            var allPackages = PackageSupplier.GetPackages();

            var notificationPackages = allPackages
                .OfType<Package<SendsNotification, TIssueType>>()
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

            // Self-updates (REST calls)
            var selfUpdates = allPackages
                    .OfType<Package<SelfUpdate, TIssueType>>()
                    .ToHttpRequests(PackageConverter)
                ;

            HttpHandler?.HandleAll(selfUpdates);
        }

        public async Task RunAsync()
        {
            await Task.Factory.StartNew(Run);
        }
    }
}
