using System.Collections.Generic;
using Messaging;
using JiraRest;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;

namespace Preesta.Extensions
{
    internal static class PackageExtensions
    {
        public static IEnumerable<Message> ToMessages<TIssueType>(this IEnumerable<Package<SendsNotification, TIssueType>> packages, 
            IPackageConverter<TIssueType> converter)
        {
            return converter.ToMessages(packages);
        }
        public static IEnumerable<HttpRequest> ToHttpRequests<TIssueType>(this IEnumerable<Package<SelfUpdate, TIssueType>> packages,
            IPackageConverter<TIssueType> converter)
        {
            return converter.ToHttpRequests(packages);
        }
    }
}
