using System.Collections.Generic;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;

namespace Preesta.Notification.Delivery
{
    /// <summary>
    /// One place a digest can be sent: email, a Telegram DM, a Slack DM, …
    /// Adding a new delivery target means writing one implementation and
    /// registering it in <see cref="DeliveryChannels"/> — nothing in the
    /// pipeline changes. The interface is non-generic with a generic
    /// <see cref="Deliver{TIssueType}"/> method so a single channel instance
    /// serves both the Issue and Release pipelines.
    /// </summary>
    internal interface IDeliveryChannel
    {
        /// <summary>Short label used in the pipeline's per-stage error log.</summary>
        string Name { get; }

        void Deliver<TIssueType>(
            IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            IPackageConverter<TIssueType> converter);
    }
}
