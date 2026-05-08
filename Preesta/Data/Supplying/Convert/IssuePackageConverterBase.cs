using System.Collections.Generic;
using Messaging;
using JiraRest;
using Preesta.Notification;

namespace Preesta.Data.Supplying.Convert
{
    internal abstract class PackageConverterBase<TItemType> : IPackageConverter<TItemType>
    {
        public string SubjectPrefix { get; set; } = "[Jira] Unprocessed Issues ";

        protected PackageConverterBase(string subjectPrefix = "[Jira] Unprocessed Issues ")
        {
            SubjectPrefix = subjectPrefix;
        }

        public Message[] ToMessages(IEnumerable<Package<NotificationReaction, TItemType>> packages)
        {
            return MessageBuilder<TItemType>.ToMessage(packages, FormatHtml, FormatText, SubjectPrefix);
        }

        public Message[] ToTelegramMessages(IEnumerable<Package<NotificationReaction, TItemType>> packages,
            Redirector redirector,
            IReadOnlyDictionary<string, string> telegramUserMap)
        {
            return MessageBuilder<TItemType>.ToTelegramMessages(packages, FormatText, SubjectPrefix, redirector, telegramUserMap);
        }

        public abstract HttpRequest[] ToHttpRequests(
            IEnumerable<Package<SelfUpdate, TItemType>> packages);

        public virtual string[] ToGraphQLMutationBodies(
            IEnumerable<Package<GraphQLMutation, TItemType>> packages) =>
            new string[] { };

        protected internal abstract string FormatHtml(IEnumerable<Package<NotificationReaction, TItemType>> packages);
        protected internal abstract string FormatText(IEnumerable<Package<NotificationReaction, TItemType>> packages);
    }
}
