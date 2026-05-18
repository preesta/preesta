using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PlaneRest
{
    /// <summary>
    /// Minimal Plane REST gateway used by Preesta. Three methods cover the read +
    /// member-lookup path; mutations go through the lower-level <see cref="Send"/>.
    /// Mirror of <c>GithubGraphQL.IGithubGateway</c> in spirit, but REST rather
    /// than GraphQL — Plane has no public GraphQL surface.
    /// </summary>
    public interface IPlaneGateway
    {
        /// <summary>
        /// GET <c>/api/v1/workspaces/{slug}/projects/{projectId}/work-items/</c>
        /// with the given query parameters merged into the URL. Returns the raw
        /// paginated envelope (<c>{ results: [...], next_cursor, ... }</c>).
        /// </summary>
        JObject ListWorkItems(string projectId, IReadOnlyDictionary<string, string> queryParams);

        /// <summary>
        /// GET <c>/api/v1/workspaces/{slug}/members/</c>. Returns the raw JSON
        /// (an array). Used once at startup to build the UUID → email map for
        /// assignee / reporter routing.
        /// </summary>
        JArray ListWorkspaceMembers();

        /// <summary>
        /// Issues an arbitrary HTTP request (verb + absolute URI + JSON body).
        /// Used by <c>PlaneMutationExecutor</c> for rule-defined mutations.
        /// Returns the raw response body as string for logging; the caller is
        /// responsible for status-code handling (the implementation throws on
        /// non-2xx).
        /// </summary>
        string Send(string verb, System.Uri uri, string? body);
    }
}
