using System.Collections.Generic;
using Messaging;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;

namespace Preesta.Notification.Delivery
{
    /// <summary>
    /// Slack personal-DM delivery. The email→Slack-user-id map turns resolved
    /// recipient emails into Slack user IDs; redirection is applied inside the
    /// converter's <c>ToSlackMessages</c> path.
    /// </summary>
    internal sealed class SlackChannel : IDeliveryChannel
    {
        private readonly IMessenger _messenger;
        private readonly Redirector _redirector;
        private readonly IReadOnlyDictionary<string, string> _userMap;

        public SlackChannel(IMessenger messenger, Redirector redirector,
            IReadOnlyDictionary<string, string> userMap)
        {
            _messenger = messenger;
            _redirector = redirector;
            _userMap = userMap;
        }

        public string Name => "slack";

        public void Deliver<TIssueType>(
            IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            IPackageConverter<TIssueType> converter)
        {
            _messenger.SendAll(packages.ToSlackMessages(converter, _redirector, _userMap));
        }
    }
}
