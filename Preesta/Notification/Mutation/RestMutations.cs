using System.Collections.Generic;
using System.Linq;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;

namespace Preesta.Notification.Mutation
{
    /// <summary>
    /// REST mutation transport (Jira <c>callRest</c>, Shortcut). Pulls the
    /// <see cref="SelfUpdate"/> packages out of the run, converts them to
    /// HTTP requests, and hands them to the underlying <see cref="IHttpHandler"/>.
    /// </summary>
    internal sealed class RestMutations : IMutationHandler
    {
        private readonly IHttpHandler _handler;

        public RestMutations(IHttpHandler handler) => _handler = handler;

        public void Execute<TIssueType>(
            IEnumerable<PackageBase> allPackages,
            IPackageConverter<TIssueType> converter)
        {
            var requests = allPackages
                .OfType<Package<SelfUpdate, TIssueType>>()
                .ToHttpRequests(converter);
            _handler.HandleAll(requests);
        }
    }
}
