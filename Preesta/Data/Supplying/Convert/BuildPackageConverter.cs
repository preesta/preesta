using System.Collections.Generic;
using Preesta.Formatting;
using JiraRest;

namespace Preesta.Data.Supplying.Convert
{
    internal class BuildPackageConverter : PackageConverterBase<Build>
    {
        public BuildPackageConverter(string subjectPrefix = "[Jira] Unprocessed Issues ")
            : base(subjectPrefix)
        {
        }

        public override HttpRequest[] ToHttpRequests(IEnumerable<Package<SelfUpdate, Build>> packages)
        {
            return new HttpRequest[]{};
        }

        protected internal override string FormatHtml(IEnumerable<Package<SendsNotification, Build>> packages)
        {
            return BuildFormatter.ToHtml(packages);
        }

        protected internal override string FormatText(IEnumerable<Package<SendsNotification, Build>> packages)
        {
            return BuildFormatter.ToText(packages);
        }
    }
}
