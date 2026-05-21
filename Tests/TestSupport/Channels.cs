using System.Collections.Generic;
using Messaging;
using Preesta.Notification;
using Preesta.Notification.Delivery;

namespace Tests.TestSupport
{
    /// <summary>
    /// Concise <see cref="DeliveryChannels"/> builders for tests. Production
    /// code assembles channels via <see cref="DeliveryChannels.Build"/> in DI;
    /// tests usually care about one or two channels and an optional redirector.
    /// </summary>
    internal static class Channels
    {
        private static readonly IReadOnlyDictionary<string, string> NoUsers =
            new Dictionary<string, string>();

        public static DeliveryChannels Email(IMessenger email, Redirector? redirector = null) =>
            DeliveryChannels.Build(email, null, NoUsers, null, NoUsers,
                redirector ?? Redirector.Empty, logoFileName: "");

        public static DeliveryChannels Of(
            IMessenger? email = null,
            IMessenger? telegram = null,
            IMessenger? slack = null,
            Redirector? redirector = null) =>
            DeliveryChannels.Build(email, telegram, NoUsers, slack, NoUsers,
                redirector ?? Redirector.Empty, logoFileName: "");
    }
}
