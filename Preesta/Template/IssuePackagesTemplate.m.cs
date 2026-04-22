using System.Collections.Generic;
using Preesta.Data;
using Preesta.Data.Supplying;

namespace Preesta.Template
{
    using IssuePackage = Package<SendsNotification, Issue>;
    public partial class IssuePackagesTemplate
    {
        private readonly IEnumerable<IssuePackage> _packages;
        private readonly string _rootUri;

        internal IssuePackagesTemplate(IEnumerable<IssuePackage> packages, string rootUri)
        {
            _packages = packages;
            _rootUri = rootUri;
        }
    }
}