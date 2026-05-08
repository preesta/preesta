using System.Collections.Generic;
using Preesta.Formatting;
using JiraRest;

namespace Preesta.Data.Supplying.Convert
{
    internal class ReleasePackageConverter : PackageConverterBase<Release>
    {
        public ReleasePackageConverter(string subjectPrefix = "[Jira] Unprocessed Issues ")
            : base(subjectPrefix)
        {
        }

        public override HttpRequest[] ToHttpRequests(IEnumerable<Package<SelfUpdate, Release>> packages)
        {
            return new HttpRequest[]{};
        }

        protected internal override string FormatHtml(IEnumerable<Package<NotificationReaction, Release>> packages)
        {
            return ReleaseFormatter.ToHtml(packages);
        }

        protected internal override string FormatText(IEnumerable<Package<NotificationReaction, Release>> packages)
        {
            return ReleaseFormatter.ToText(packages);
        }
    }
}
