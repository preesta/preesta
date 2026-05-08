using Newtonsoft.Json.Linq;

namespace LinearGraphQL
{
    /// <summary>
    /// Minimal Linear gateway used by Preesta.
    /// Future-proof but intentionally small for the MVP.
    /// </summary>
    public interface ILinearGateway
    {
        /// <summary>
        /// Returns the raw GraphQL response (full envelope including <c>data</c> and
        /// optional <c>errors</c> array) for the assigned-issues query.
        /// </summary>
        JObject GetAssignedIssues();
    }
}
