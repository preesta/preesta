using System.Collections.Generic;
using Messaging;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;

namespace Preesta.Notification.Delivery
{
    /// <summary>
    /// Telegram personal-DM delivery. The email→chatId map turns resolved
    /// recipient emails into Telegram chat IDs; redirection is applied inside
    /// the converter's <c>ToTelegramMessages</c> path.
    /// </summary>
    internal sealed class TelegramChannel : IDeliveryChannel
    {
        private readonly IMessenger _messenger;
        private readonly Redirector _redirector;
        private readonly IReadOnlyDictionary<string, string> _userMap;

        public TelegramChannel(IMessenger messenger, Redirector redirector,
            IReadOnlyDictionary<string, string> userMap)
        {
            _messenger = messenger;
            _redirector = redirector;
            _userMap = userMap;
        }

        public string Name => "telegram";

        public void Deliver<TIssueType>(
            IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            IPackageConverter<TIssueType> converter)
        {
            _messenger.SendAll(packages.ToTelegramMessages(converter, _redirector, _userMap));
        }
    }
}
