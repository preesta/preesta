using System.Collections.Generic;

namespace Preesta.Configuration.Action
{
    /// <summary>
    /// Wires <see cref="PlaneIssueSource"/> into the notification pipeline.
    /// </summary>
    /// <remarks>
    /// Plane's REST API requires a project ID in every work-items URL — there is no
    /// org-wide search endpoint comparable to GitHub or Linear. So a rule must always
    /// name a single project via <see cref="ProjectId"/>. Multi-project monitoring is
    /// expressed as multiple rules with the same notification settings.
    /// <para>
    /// Selection inside the project happens via <see cref="Filter"/> — a small map of
    /// Plane query-param keys to values (e.g. <c>state: backlog,unstarted</c>,
    /// <c>priority: urgent,high</c>, <c>search: "memory leak"</c>). The keys match
    /// Plane's documented list-issues query params verbatim; this is the most
    /// human-readable form we can offer without inventing a tracker-specific DSL.
    /// Filter is optional — an empty filter returns every work item in the project,
    /// which is the natural default for small projects.
    /// </para>
    /// <para>
    /// Rules are deliberately obezlichennye (impersonal): the filter must not embed
    /// assignee identity. Per-recipient routing happens later in the notification
    /// step via the standard <c>assignee</c> / <c>reporter</c> markers in
    /// <c>mailTo</c> + the workspace-level <c>slackUsers:</c> / <c>telegramUsers:</c>
    /// (email → ID) maps. The assignee resolution itself goes through the
    /// once-at-startup workspace members lookup (UUID → email).
    /// </para>
    /// <para>
    /// Mutations are raw REST requests (<c>verb</c>/<c>urlPattern</c>/<c>body</c>),
    /// same shape as Jira's <c>callRest</c>/<c>mutations</c>. The base
    /// <see cref="Rule.Mutations"/> array carries them.
    /// </para>
    /// </remarks>
    public class PlaneRule : Rule
    {
        /// <summary>
        /// Plane project ID (UUID). Required — the list-work-items endpoint is
        /// project-scoped.
        /// </summary>
        public string? ProjectId { get; set; }

        /// <summary>
        /// Map of Plane list-issues query-param keys to values. Empty/null means
        /// "all work items in the project". Keys that don't match documented Plane
        /// params are still passed through verbatim — Plane silently ignores unknown
        /// params, and pinning the set in our code would just lag behind Plane's API.
        /// </summary>
        public Dictionary<string, string> Filter { get; set; } = new Dictionary<string, string>();
    }
}
