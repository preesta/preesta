using System.Linq;
using System.Threading.Tasks;
using Messaging;
using Bender.Data.Supplying;
using Bender.Data.Supplying.Convert;
using Bender.Extensions;

namespace Bender.Notification
{
    internal class ReactionPipe<TIssueType>
    {
        private readonly IPackageSupplier? _packageSupplier;
        private readonly IPackageConverter<TIssueType>? _packageConverter;
        private readonly IMessenger? _messenger;
        private readonly IHttpHandler? _httpHandler;
        private readonly Redirector _redirector;
        private readonly string _logoFileName;

        public ReactionPipe(
            IPackageSupplier? packageSupplier = null,
            IPackageConverter<TIssueType>? packageConverter = null,
            IMessenger? messenger = null,
            IHttpHandler? httpHandler = null,
            Redirector? redirector = null,
            string logoFileName = "")
        {
            _packageSupplier = packageSupplier;
            _packageConverter = packageConverter;
            _messenger = messenger;
            _httpHandler = httpHandler;
            _redirector = redirector ?? Redirector.Empty;
            _logoFileName = logoFileName;
        }

        public void Run()
        {
            if (_packageConverter == null || _packageSupplier == null)
            {
                // Nothing to do
                return;
            }

            var allPackages = _packageSupplier.GetPackages();

            var messages =
                allPackages
                    .OfType<Package<BenderSendsLetter, TIssueType>>()
                    .ToMessages(_packageConverter)
                    .Redirect(_redirector)
                    .SetLogo(_logoFileName);

            _messenger?.SendAll(messages);

            var updatesBenderShouldMadeHimself = allPackages
                    .OfType<Package<BenderMakesUpdateHimself, TIssueType>>()
                    .ToHttpRequests(_packageConverter)
                ;

            _httpHandler?.HandleAll(updatesBenderShouldMadeHimself);
        }

        public async Task RunAsync()
        {
            await Task.Factory.StartNew(Run);
        }
    }
}
