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
        public IHttpHandler? HttpHandler { get; set; }
        public Redirector Redirector { get; set; } = Redirector.Empty;
        public string LogoFileName { get; set; } = string.Empty;

        public ReactionPipe(
            IPackageSupplier? packageSupplier = null,
            IPackageConverter<TIssueType>? packageConverter = null,
            IMessenger? messenger = null,
            IHttpHandler? httpHandler = null,
            Redirector? redirector = null,
            string logoFileName = "")
        {
            PackageSupplier = packageSupplier;
            PackageConverter = packageConverter;
            Messenger = messenger;
            HttpHandler = httpHandler;
            Redirector = redirector ?? Redirector.Empty;
            LogoFileName = logoFileName;
        }

        public void Run()
        {
            if (PackageConverter == null || PackageSupplier == null)
            {
                // Nothing to do
                return;
            }

            var allPackages = PackageSupplier.GetPackages();

            var messages =
                allPackages
                    .OfType<Package<SendsNotification, TIssueType>>()
                    .ToMessages(PackageConverter)
                    .Redirect(Redirector)
                    .SetLogo(LogoFileName);

            Messenger?.SendAll(messages);

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
