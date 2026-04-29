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

        public Message[] ToMessages(IEnumerable<Package<SendsNotification, TItemType>> packages)
        {
            return Common<TItemType>.ToMessage(packages, FormatHtml, FormatText, SubjectPrefix);
        }

        public Message[] ToTelegramMessages(IEnumerable<Package<SendsNotification, TItemType>> packages,
            Redirector redirector,
            IReadOnlyDictionary<string, string> telegramUserMap)
        {
            return Common<TItemType>.ToTelegramMessages(packages, FormatText, SubjectPrefix, redirector, telegramUserMap);
        }

        public abstract HttpRequest[] ToHttpRequests(
            IEnumerable<Package<SelfUpdate, TItemType>> packages);

        protected internal abstract string FormatHtml(IEnumerable<Package<SendsNotification, TItemType>> packages);
        protected internal abstract string FormatText(IEnumerable<Package<SendsNotification, TItemType>> packages);
    }
}
