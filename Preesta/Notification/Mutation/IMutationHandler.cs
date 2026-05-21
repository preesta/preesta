using System.Collections.Generic;
using Preesta.Data.Supplying;
using Preesta.Data.Supplying.Convert;

namespace Preesta.Notification.Mutation
{
    /// <summary>
    /// The write side of a pipeline: takes the run's packages and executes
    /// whatever mutations they carry. A tracker has exactly one mutation
    /// transport (REST for Jira/Shortcut, GraphQL for Linear/GitHub/GitLab),
    /// so a pipeline holds exactly one of these — not two nullable slots.
    /// The REST-vs-GraphQL distinction lives inside the implementation.
    /// </summary>
    internal interface IMutationHandler
    {
        void Execute<TIssueType>(
            IEnumerable<PackageBase> allPackages,
            IPackageConverter<TIssueType> converter);
    }
}
