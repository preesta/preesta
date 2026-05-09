using System.Collections.Generic;
using Messaging;
using JiraRest;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Notification;

namespace Preesta.Extensions
{
    internal static class PackageExtensions
    {
        public static IEnumerable<Message> ToMessages<TIssueType>(this IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            IPackageConverter<TIssueType> converter)
        {
            return converter.ToMessages(packages);
        }

        public static IEnumerable<Message> ToTelegramMessages<TIssueType>(this IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            IPackageConverter<TIssueType> converter,
            Redirector redirector,
            IReadOnlyDictionary<string, string> telegramUserMap)
        {
            return converter.ToTelegramMessages(packages, redirector, telegramUserMap);
        }

        public static IEnumerable<Message> ToSlackMessages<TIssueType>(this IEnumerable<Package<NotificationReaction, TIssueType>> packages,
            IPackageConverter<TIssueType> converter,
            Redirector redirector,
            IReadOnlyDictionary<string, string> slackUserMap)
        {
            return converter.ToSlackMessages(packages, redirector, slackUserMap);
        }

        public static IEnumerable<HttpRequest> ToHttpRequests<TIssueType>(this IEnumerable<Package<SelfUpdate, TIssueType>> packages,
            IPackageConverter<TIssueType> converter)
        {
            return converter.ToHttpRequests(packages);
        }

        public static IEnumerable<string> ToGraphQLMutationBodies<TIssueType>(this IEnumerable<Package<GraphQLMutation, TIssueType>> packages,
            IPackageConverter<TIssueType> converter)
        {
            return converter.ToGraphQLMutationBodies(packages);
        }
    }
}
