using System;
using System.Collections.Generic;
using System.Linq;
using Preesta.Template;
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

        protected internal override string StickThemesToSingleHtml(IEnumerable<Package<SendsNotification, Build>> packages)
        {
            return new BuildPackagesTemplate(packages).TransformText();
        }
    }
}