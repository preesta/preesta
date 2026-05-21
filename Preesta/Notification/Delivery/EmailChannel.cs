using System.Collections.Generic;
using Messaging;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;

namespace Preesta.Notification.Delivery
{
    /// <summary>
    /// SMTP delivery. Unlike the DM channels, redirection is applied as a
    /// post-processing step (<see cref="MessageExtensions.Redirect"/>) and the
    /// digest logo is attached afterwards.
    /// </summary>
    internal sealed class EmailChannel : IDeliveryChannel
    {
        private readonly IMessenger _messenger;
        private readonly Redirector _redirector;
        private readonly string _logoFileName;

        public EmailChannel(IMessenger messenger, Redirector redirector, string logoFileName)
        {
            _messenger = messenger;
            _redirector = redirector;
            _logoFileName = logoFileName;
        }

        public string Name => "email";

        public void Deliver<TIssueType>(
            IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            IPackageConverter<TIssueType> converter)
        {
            var messages = packages
                .ToMessages(converter)
                .Redirect(_redirector)
                .SetLogo(_logoFileName);
            _messenger.SendAll(messages);
        }
    }
}
