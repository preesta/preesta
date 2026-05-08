using System.Collections.Generic;
using Messaging;
using JiraRest;
using Preesta.Notification;

namespace Preesta.Data.Supplying.Convert
{
    internal interface IPackageConverter<TIssueType>
    {
        Message[] ToMessages(IEnumerable<Package<NotificationReaction, TIssueType>> packages);
        Message[] ToTelegramMessages(IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            Redirector redirector,
            IReadOnlyDictionary<string, string> telegramUserMap);
        HttpRequest[] ToHttpRequests(IEnumerable<Package<SelfUpdate, TIssueType>> packages);
    }
}
