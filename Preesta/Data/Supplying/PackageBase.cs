using System.Collections.Generic;

namespace Preesta.Data.Supplying
{
    internal abstract class PackageBase
    {
        public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
}