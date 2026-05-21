using System.Collections.Generic;

namespace Preesta.Notification.Delivery
{
    /// <summary>
    /// The set of delivery targets a digest fans out to, assembled once at
    /// startup and shared by every pipeline. Only configured channels are
    /// present, so the pipeline simply iterates whatever is here — it never
    /// asks "is email configured?".
    /// </summary>
    internal sealed class DeliveryChannels
    {
        public IReadOnlyList<IDeliveryChannel> Channels { get; }

        public DeliveryChannels(IReadOnlyList<IDeliveryChannel> channels)
        {
            Channels = channels;
        }

        /// <summary>An empty set — used by tests that exercise only the
        /// mutation path, and as a safe default.</summary>
        public static readonly DeliveryChannels None =
            new DeliveryChannels(new List<IDeliveryChannel>());

        /// <summary>
        /// Builds the channel list from whatever messengers are configured.
        /// A null messenger means that channel isn't set up and is skipped.
        /// Adding a new target (Discord, webhook, …) is one more conditional
        /// here plus its <see cref="IDeliveryChannel"/> implementation.
        /// </summary>
        public static DeliveryChannels Build(
            Messaging.IMessenger? email,
            Messaging.IMessenger? telegram,
            IReadOnlyDictionary<string, string> telegramUsers,
            Messaging.IMessenger? slack,
            IReadOnlyDictionary<string, string> slackUsers,
            Redirector redirector,
            string logoFileName)
        {
            var channels = new List<IDeliveryChannel>();
            if (email != null)
                channels.Add(new EmailChannel(email, redirector, logoFileName));
            if (telegram != null)
                channels.Add(new TelegramChannel(telegram, redirector, telegramUsers));
            if (slack != null)
                channels.Add(new SlackChannel(slack, redirector, slackUsers));
            return new DeliveryChannels(channels);
        }
    }
}
