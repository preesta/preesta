using System.Collections.Generic;
using System.Linq;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;
using Preesta.Extensions;

namespace Preesta.Notification.Mutation
{
    /// <summary>
    /// GraphQL mutation transport (Linear, GitHub, GitLab). Pulls the
    /// <see cref="GraphQLMutation"/> packages out of the run, renders their
    /// mutation bodies, and hands them to the underlying
    /// <see cref="IGraphQLMutationHandler"/>.
    /// </summary>
    internal sealed class GraphQLMutations : IMutationHandler
    {
        private readonly IGraphQLMutationHandler _handler;

        public GraphQLMutations(IGraphQLMutationHandler handler) => _handler = handler;

        public void Execute<TIssueType>(
            IEnumerable<PackageBase> allPackages,
            IPackageConverter<TIssueType> converter)
        {
            var bodies = allPackages
                .OfType<Package<GraphQLMutation, TIssueType>>()
                .ToGraphQLMutationBodies(converter);
            _handler.HandleAll(bodies);
        }
    }
}
