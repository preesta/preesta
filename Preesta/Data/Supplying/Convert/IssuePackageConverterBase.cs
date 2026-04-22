using System.Collections.Generic;
using Messaging;
using JiraRest;

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
            return Common<TItemType>.ToMessage(packages, StickThemesToSingleHtml, SubjectPrefix);
        }

        public abstract HttpRequest[] ToHttpRequests(
            IEnumerable<Package<SelfUpdate, TItemType>> packages);

        protected internal abstract string StickThemesToSingleHtml(IEnumerable<Package<SendsNotification, TItemType>> packages);
    }
}