using System.Collections.Generic;
using Preesta.Data;
using Preesta.Data.Supplying;

namespace Preesta.Template
{
    public partial class BuildPackagesTemplate
    {
        private IEnumerable<Package<SendsNotification, Build>> Packages { get; }

        internal BuildPackagesTemplate(IEnumerable<Package<SendsNotification, Build>> packages)
        {
            Packages = packages;
        }
    }
}