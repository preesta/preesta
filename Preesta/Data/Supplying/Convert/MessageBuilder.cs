using System;
using System.Collections.Generic;
using System.Linq;
using Messaging;
using Preesta.Notification;
using static System.String;

namespace Preesta.Data.Supplying.Convert
{
    internal static class MessageBuilder<TIssueType>
    {
        public static Message[] ToMessage(IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            Func<IEnumerable<Package<NotificationReaction, TIssueType>>, string> toHtml,
            Func<IEnumerable<Package<NotificationReaction, TIssueType>>, string> toText,
            string subjectPrefix)
        {
            string ToOrderedString(IEnumerable<string> a) => Join(",", a.OrderBy(c => c).ToArray());
            return (from package in packages
                    group package by new
                                     {
                                         To = ToOrderedString(package.Reaction.Addressees.To),
                                         Cc = ToOrderedString(package.Reaction.Addressees.Cc),
                                         package.Reaction.Subject
                                     }
                    into ag
                    select new Message
                           {
                               To = ag.Key.To,
                               Cc = ag.Key.Cc,

                               Subject = $"{subjectPrefix}{ag.Key.Subject}",

                               IsBodyHtml = true,
                               Body = toHtml(ag),
                               TextBody = toText(ag)
                           }).ToArray();
        }

        public static Message[] ToTelegramMessages(IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            Func<IEnumerable<Package<NotificationReaction, TIssueType>>, string> toText,
            string subjectPrefix,
            Redirector redirector,
            IReadOnlyDictionary<string, string> telegramUserMap)
        {
            var userMap = new Dictionary<string, string>(telegramUserMap, StringComparer.OrdinalIgnoreCase);
            var packagesArr = packages.ToArray();
            if (packagesArr.Length == 0)
                return System.Array.Empty<Message>();

            var packageChatIds =
                from p in packagesArr
                let resolvedEmails = redirector.ResolveRecipients(
                    p.Reaction.Addressees.To.Concat(p.Reaction.Addressees.Cc))
                let mappedChatIds = resolvedEmails
                    .Where(userMap.ContainsKey)
                    .Select(e => userMap[e])
                let allChatIds = mappedChatIds
                    .Concat(p.Reaction.TelegramChatIds)
                    .Distinct()
                from chatId in allChatIds
                select new { Package = p, ChatId = chatId };

            return (from pwc in packageChatIds
                    group pwc.Package by pwc.ChatId into g
                    let subject = g.First().Reaction.Subject
                    select new Message
                    {
                        To = g.Key,
                        Subject = $"{subjectPrefix}{subject}",
                        TextBody = toText(g)
                    }).ToArray();
        }

        public static Message[] ToSlackMessages(IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            Func<IEnumerable<Package<NotificationReaction, TIssueType>>, string> toMrkdwn,
            string subjectPrefix,
            Redirector redirector,
            IReadOnlyDictionary<string, string> slackUserMap)
        {
            var userMap = new Dictionary<string, string>(slackUserMap, StringComparer.OrdinalIgnoreCase);
            var packagesArr = packages.ToArray();
            if (packagesArr.Length == 0)
                return System.Array.Empty<Message>();

            var packageUserIds =
                from p in packagesArr
                let resolvedEmails = redirector.ResolveRecipients(
                    p.Reaction.Addressees.To.Concat(p.Reaction.Addressees.Cc))
                let mappedUserIds = resolvedEmails
                    .Where(userMap.ContainsKey)
                    .Select(e => userMap[e])
                let allUserIds = mappedUserIds
                    .Concat(p.Reaction.SlackUserIds)
                    .Distinct()
                from userId in allUserIds
                select new { Package = p, UserId = userId };

            return (from pwu in packageUserIds
                    group pwu.Package by pwu.UserId into g
                    let subject = g.First().Reaction.Subject
                    select new Message
                    {
                        To = g.Key,
                        Subject = $"{subjectPrefix}{subject}",
                        TextBody = toMrkdwn(g)
                    }).ToArray();
        }
    }
}
